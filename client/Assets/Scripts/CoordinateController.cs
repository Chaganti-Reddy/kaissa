using System;
using System.Collections;
using System.Linq;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Coordinates trainer (chess.com's Coordinates drill): a square name is shown, click it on the board as
// fast as you can inside 30 seconds. Choose your side (board orientation) and whether the a-h/1-8 labels
// are shown (hide them to really train recall). Correct flashes green; a miss flashes the wrong square
// red and reveals the right one. Per-run best score is kept. 2D board (needs square clicks).
public sealed class CoordinateController : MonoBehaviour
{
    private const float RunSeconds = 30f;

    private CoordinateSession _session;
    private Board2D _board;
    private string _target = "";
    private bool _running, _busy, _whiteBottom = true, _showLabels;
    private float _timeLeft;

    private Label _prompt, _timerLabel, _scoreLabel, _bestLabel, _feedback;
    private VisualElement _root, _overlayHost;

    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.60f);
    private static readonly Color Bad = new(0.92f, 0.33f, 0.33f, 0.60f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _board = new Board2D(null) { ShowCoordinates = false };
        _board.SquareClickHandler = OnSquareClicked;

        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        StartCoroutine(Build(doc));
    }

    private IEnumerator Build(UIDocument doc)
    {
        yield return null;
        _root = doc.rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Row; _root.style.flexGrow = 1; _root.style.backgroundColor = UiKit.Bg;
        _root.Add(UiKit.NavRail("Coordinate"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 8;
        _prompt = UiKit.Text_("Coordinates", 30, UiKit.Text, bold: true);
        head.Add(_prompt);
        _timerLabel = UiKit.Text_("", 24, UiKit.Gold, bold: true);
        head.Add(_timerLabel);
        center.Add(head);

        var host = new VisualElement();
        host.style.width = 480; host.style.height = 480; host.style.flexShrink = 0;
        host.Add(_board.Root);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        center.Add(host);

        _feedback = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 8; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 4, 12, 4, 12); UiKit.Radius(_feedback, 8);
        center.Add(_feedback);
        _root.Add(center);

        var rail = new VisualElement();
        rail.style.width = 300; UiKit.Pad(rail, 18, 24, 18, 8);
        var panel = Panel(); UiKit.Pad(panel, 16);
        panel.Add(UiKit.Text_("SCORE", 11, UiKit.Mute, bold: true));
        _scoreLabel = UiKit.Text_("0", 40, UiKit.Text, bold: true);
        panel.Add(_scoreLabel);
        _bestLabel = UiKit.Text_($"Best {KaissaSettings.CoordBest}", 13, UiKit.Dim, bold: true);
        panel.Add(_bestLabel);
        rail.Add(panel);
        _root.Add(rail);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        RenderBoard();

        if (Environment.GetCommandLineArgs().Contains("-kaissa-coordtest"))
            StartCoroutine(AutoDemo());
        else
            ShowStartOverlay();
    }

    // ---------------- run lifecycle ----------------

    private void ShowStartOverlay()
    {
        _running = false;
        _timerLabel.text = ""; SetFeedback("", UiKit.Dim);
        var dim = Overlay();
        var panel = Panel(); UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center; panel.style.width = 420;
        panel.Add(UiKit.Text_("Coordinates", 28, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Click the square shown, as many as you can in 30 seconds.", 14, UiKit.Dim);
        sub.style.marginTop = 8; sub.style.marginBottom = 16; sub.style.whiteSpace = WhiteSpace.Normal; sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);

        var sideRow = UiKit.Row(); sideRow.style.marginBottom = 10;
        Button whiteBtn = null, blackBtn = null;
        whiteBtn = OptBtn("Play White", () => { _whiteBottom = true; Mark(whiteBtn, blackBtn, true); });
        blackBtn = OptBtn("Play Black", () => { _whiteBottom = false; Mark(whiteBtn, blackBtn, false); });
        Mark(whiteBtn, blackBtn, _whiteBottom);
        sideRow.Add(whiteBtn); sideRow.Add(blackBtn);
        panel.Add(sideRow);

        Button labelsBtn = null;
        labelsBtn = OptBtn(LabelText(), () => { _showLabels = !_showLabels; labelsBtn.text = LabelText(); });
        labelsBtn.style.marginBottom = 16;
        panel.Add(labelsBtn);

        var start = UiKit.Primary("Start", StartRun, 16); start.style.width = 320;
        panel.Add(start);
        dim.Add(panel);
        _overlayHost.Add(dim);
    }

    private string LabelText() => _showLabels ? "Coordinates: shown" : "Coordinates: hidden";

    private static void Mark(Button a, Button b, bool first)
    {
        a.style.backgroundColor = first ? UiKit.Green : UiKit.Panel2;
        b.style.backgroundColor = first ? UiKit.Panel2 : UiKit.Green;
    }

    private Button OptBtn(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 13);
        b.style.marginLeft = 4; b.style.marginRight = 4; UiKit.Pad(b, 8, 12, 8, 12);
        return b;
    }

    private void StartRun()
    {
        _overlayHost.Clear();
        _session = new CoordinateSession();
        _timeLeft = RunSeconds; _running = true; _busy = false;
        _board.ShowCoordinates = _showLabels;
        _scoreLabel.text = "0";
        _timerLabel.text = Fmt(RunSeconds);
        SetFeedback("", UiKit.Dim);
        RenderBoard();
        Next();
    }

    private void OnSquareClicked(string square)
    {
        if (!_running || _busy) return;
        var (file, rank) = Coordinates.Parse(square);
        bool correct = _session.Answer(file, rank);
        _scoreLabel.text = _session.Score.ToString();

        if (correct)
        {
            _board.HighlightSquare(square, Good);
            SetFeedback("", UiKit.Dim);
            Next(); // instant next; the green flash clears on the re-render
        }
        else
        {
            _busy = true;
            _board.HighlightSquare(square, Bad);
            _board.HighlightSquare(_target, Good); // reveal the right square
            SetFeedback($"That was {square}. {_target} is here.", UiKit.Danger);
            StartCoroutine(ResumeAfter(0.9f));
        }
    }

    private IEnumerator ResumeAfter(float s)
    {
        yield return new WaitForSeconds(s);
        _busy = false;
        if (_running) Next();
    }

    private void Next()
    {
        _target = _session.NextTarget();
        _prompt.text = _target;
        RenderBoard();
    }

    private void RenderBoard() =>
        _board.Render("8/8/8/8/8/8/8/8 w - - 0 1", canMove: false, lastMove: null, whiteBottom: _whiteBottom);

    private void EndRun()
    {
        _running = false;
        int score = _session?.Score ?? 0;
        bool record = score > KaissaSettings.CoordBest;
        if (record) KaissaSettings.CoordBest = score;
        _bestLabel.text = $"Best {KaissaSettings.CoordBest}";
        _timerLabel.text = "0:00";

        var dim = Overlay();
        var panel = Panel(); UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center; panel.style.width = 420;
        panel.Add(UiKit.Text_("Time", 15, UiKit.Dim, bold: true));
        panel.Add(UiKit.Text_(score.ToString(), 56, UiKit.Text, bold: true));
        panel.Add(UiKit.Text_("squares found", 13, UiKit.Mute));
        if (record) { var b = UiKit.Text_("New best", 15, UiKit.Gold, bold: true); b.style.marginTop = 8; panel.Add(b); }
        var again = UiKit.Primary("Play again", StartRun, 15); again.style.width = 320; again.style.marginTop = 16; again.style.marginBottom = 8;
        panel.Add(again);
        var change = UiKit.Ghost("Change side", ShowStartOverlay); change.style.width = 320; change.style.marginBottom = 8;
        panel.Add(change);
        var menu = UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu")); menu.style.width = 320;
        panel.Add(menu);
        dim.Add(panel);
        _overlayHost.Add(dim);
    }

    // ---------------- helpers ----------------

    private VisualElement Overlay()
    {
        _overlayHost.Clear();
        var dim = new VisualElement();
        dim.style.flexGrow = 1; dim.style.backgroundColor = new Color(0, 0, 0, 0.80f);
        dim.style.alignItems = Align.Center; dim.style.justifyContent = Justify.Center;
        return dim;
    }

    private static VisualElement Panel()
    {
        var p = new VisualElement();
        p.style.backgroundColor = UiKit.Panel;
        p.style.borderTopWidth = p.style.borderBottomWidth = p.style.borderLeftWidth = p.style.borderRightWidth = 1;
        p.style.borderTopColor = p.style.borderBottomColor = p.style.borderLeftColor = p.style.borderRightColor = UiKit.Line;
        UiKit.Radius(p, 12);
        return p;
    }

    private void SetFeedback(string text, Color color)
    {
        _feedback.text = text; _feedback.style.color = color;
        _feedback.style.backgroundColor = string.IsNullOrEmpty(text) ? new Color(0, 0, 0, 0) : new Color(0, 0, 0, 0.55f);
    }

    private static string Fmt(float s) { int t = Mathf.Max(0, Mathf.CeilToInt(s)); return $"{t / 60}:{t % 60:00}"; }

    private void Update()
    {
        if (_running)
        {
            _timeLeft -= Time.deltaTime;
            _timerLabel.text = Fmt(_timeLeft);
            if (_timeLeft <= 0f) EndRun();
        }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneTransition.Go("Menu");
    }

    // ---------------- self-test ----------------

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);

        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "coord_warmup.png"));
        yield return new WaitForSeconds(0.6f);
        ShowStartOverlay();
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "coord_start.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Start"));
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "coord_playing.png"));
        yield return new WaitForSeconds(0.4f);

        // A correct answer (tap the target square) then a wrong one, through the real square-click path.
        _board.DebugTapSquare(_target);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "coord_correct.png"));
        yield return new WaitForSeconds(0.4f);
        string wrong = _target == "a1" ? "h8" : "a1";
        _board.DebugTapSquare(wrong);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "coord_wrong.png"));
        yield return new WaitForSeconds(0.6f);

        // Fast-forward to the end-of-run overlay.
        _timeLeft = 0.1f;
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "coord_gameover.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Play again"));
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "coord_again.png"));
        yield return new WaitForSeconds(0.4f);
        Application.Quit();
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key) return args[i + 1];
        return null;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
