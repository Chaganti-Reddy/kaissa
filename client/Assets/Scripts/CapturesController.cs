using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Captures & threats drill (board vision + tactics): a real position is shown; click every enemy piece
// the side to move can capture, then Submit. Solve as many as you can in 30 seconds. Trains instant
// recognition of what is hanging - the tactical half of pattern recognition. Per-run best is kept.
public sealed class CapturesController : MonoBehaviour
{
    private const float RunSeconds = 30f;

    private Board2D _board;
    private List<Scenario> _positions;
    private readonly HashSet<string> _targets = new();   // enemy squares that can be captured
    private readonly HashSet<string> _selected = new();
    private string _curFen = "";
    private bool _running, _busy, _whiteBottom = true;
    private float _timeLeft;
    private int _score;
    private readonly System.Random _rng = new();

    private VisualElement _root, _overlayHost, _boardHost, _sideBadge, _sideDot;
    private Label _sideText, _timerLabel, _scoreLabel, _bestLabel, _feedback;

    private static readonly Color Sel = new(0.36f, 0.55f, 0.86f, 0.60f);   // picked square
    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.55f);
    private static readonly Color Bad = new(0.92f, 0.33f, 0.33f, 0.55f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }
        _positions = ScenarioLibrary.LoadDefault().AllScenarios.ToList();
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
        _root.Add(UiKit.NavRail("Captures"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 8;
        _sideBadge = MakeSideBadge();
        _timerLabel = UiKit.Text_("", 22, UiKit.Gold, bold: true);
        head.Add(_sideBadge); head.Add(_timerLabel);
        center.Add(head);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        _board = new Board2D(null);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        _boardHost.Add(_board.Root);
        center.Add(_boardHost);

        var submit = UiKit.Primary("Submit", Submit, 16); submit.style.width = 220; submit.style.marginTop = 10;
        center.Add(submit);

        _feedback = UiKit.Text_("", 15, UiKit.Dim, bold: true);
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
        _bestLabel = UiKit.Text_($"Best {KaissaSettings.CapturesBest}", 13, UiKit.Dim, bold: true);
        panel.Add(_bestLabel);
        rail.Add(panel);
        _root.Add(rail);

        _board.Render(ChessGame.StartFen, canMove: false, lastMove: null, whiteBottom: true);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-capturestest"))
            StartCoroutine(AutoDemo());
        else
            ShowIntro();
    }

    private void ShowIntro()
    {
        var dim = Overlay();
        var panel = OverlayPanel(); panel.style.width = 460;
        panel.Add(UiKit.Text_("Captures & Threats", 26, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Click every enemy piece the side to move can capture, then Submit. Solve as many positions as you can in 30 seconds.", 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 10; sub.style.marginBottom = 18;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var start = UiKit.Primary("Start", StartRun, 16); start.style.width = 320;
        panel.Add(start);
        dim.Add(panel); _overlayHost.Add(dim);
    }

    private void StartRun()
    {
        _overlayHost.Clear();
        _score = 0; _scoreLabel.text = "0";
        _running = true; _busy = false;
        _timeLeft = RunSeconds;
        NextPosition();
    }

    private void NextPosition()
    {
        _selected.Clear();
        SetFeedback("", UiKit.Dim);
        // Pick a random position that actually has a capture available (retry a few times).
        for (int tries = 0; tries < 40; tries++)
        {
            var s = _positions[_rng.Next(_positions.Count)];
            ComputeTargets(s.Fen);
            if (_targets.Count > 0)
            {
                _curFen = s.Fen;
                _whiteBottom = IsWhiteToMove(s.Fen);
                UpdateSideBadge(s.Fen);
                _board.SquareClickHandler = OnPick;
                _board.Render(_curFen, canMove: false, lastMove: null, whiteBottom: _whiteBottom);
                return;
            }
        }
    }

    private void Update()
    {
        if (!_running) return;
        _timeLeft -= Time.deltaTime;
        _timerLabel.text = Mathf.CeilToInt(Mathf.Max(0, _timeLeft)).ToString();
        if (_timeLeft <= 0) GameOver();
    }

    private void OnPick(string sq)
    {
        if (!_running || _busy) return;
        if (!_selected.Add(sq)) _selected.Remove(sq); // toggle
        Redraw();
    }

    private void Redraw()
    {
        _board.Render(_curFen, canMove: false, lastMove: null, whiteBottom: _whiteBottom);
        foreach (var sq in _selected) _board.HighlightSquare(sq, Sel);
    }

    private void Submit()
    {
        if (!_running || _busy) return;
        bool correct = _selected.SetEquals(_targets);
        if (correct)
        {
            _score++; _scoreLabel.text = _score.ToString();
            SetFeedback("Correct!", UiKit.GreenHi);
        }
        else
        {
            SetFeedback("Not quite - the captures are shown", UiKit.Danger);
            foreach (var sq in _targets) _board.HighlightSquare(sq, Good);
        }
        StartCoroutine(NextAfter(correct ? 0.35f : 1.1f));
    }

    private IEnumerator NextAfter(float s)
    {
        _busy = true;
        yield return new WaitForSeconds(s);
        _busy = false;
        if (_running) NextPosition();
    }

    private void GameOver()
    {
        _running = false;
        bool record = _score > KaissaSettings.CapturesBest;
        if (record) KaissaSettings.CapturesBest = _score;
        _bestLabel.text = $"Best {KaissaSettings.CapturesBest}";

        var dim = Overlay();
        var panel = OverlayPanel(); panel.style.width = 400;
        panel.Add(UiKit.Text_("Time!", 24, UiKit.Text, bold: true));
        panel.Add(UiKit.Text_(_score.ToString(), 56, UiKit.Text, bold: true));
        panel.Add(UiKit.Text_("positions solved", 13, UiKit.Mute));
        if (record) { var b = UiKit.Text_("New personal best", 15, UiKit.Gold, bold: true); b.style.marginTop = 8; panel.Add(b); }
        var again = UiKit.Primary("Play again", StartRun, 15); again.style.width = 300; again.style.marginTop = 16; again.style.marginBottom = 8;
        panel.Add(again);
        panel.Add(UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu"), 14));
        dim.Add(panel); _overlayHost.Add(dim);
    }

    // Enemy pieces (opposite of the side to move) that a legal move can capture.
    private void ComputeTargets(string fen)
    {
        _targets.Clear();
        HashSet<string> occupied;
        IReadOnlyList<string> moves;
        try
        {
            occupied = OccupiedSquares(fen);
            moves = ChessGame.FromFen(fen).LegalUciMoves();
        }
        catch { return; }
        foreach (var uci in moves)
        {
            if (uci.Length < 4) continue;
            string to = uci.Substring(2, 2);
            if (occupied.Contains(to)) _targets.Add(to); // a capture lands on an occupied square
        }
    }

    private static HashSet<string> OccupiedSquares(string fen)
    {
        var set = new HashSet<string>();
        var placement = fen.Split(' ')[0];
        int rank = 7;
        foreach (var row in placement.Split('/'))
        {
            int file = 0;
            foreach (char c in row)
            {
                if (char.IsDigit(c)) file += c - '0';
                else { set.Add($"{(char)('a' + file)}{(char)('1' + rank)}"); file++; }
            }
            rank--;
        }
        return set;
    }

    private static bool IsWhiteToMove(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length < 2 || parts[1] == "w";
    }

    private VisualElement MakeSideBadge()
    {
        _sideDot = new VisualElement();
        _sideDot.style.width = 12; _sideDot.style.height = 12; UiKit.Radius(_sideDot, 6); _sideDot.style.marginRight = 8;
        _sideDot.style.borderTopWidth = _sideDot.style.borderBottomWidth = _sideDot.style.borderLeftWidth = _sideDot.style.borderRightWidth = 1;
        _sideDot.style.borderTopColor = _sideDot.style.borderBottomColor = _sideDot.style.borderLeftColor = _sideDot.style.borderRightColor = UiKit.Line;
        _sideText = UiKit.Text_("", 16, UiKit.Text, bold: true);
        var row = UiKit.Row(_sideDot, _sideText);
        UiKit.Pad(row, 6, 12, 6, 12); UiKit.Radius(row, 6);
        return row;
    }

    private void UpdateSideBadge(string fen)
    {
        bool white = IsWhiteToMove(fen);
        _sideText.text = white ? "White to capture" : "Black to capture";
        _sideDot.style.backgroundColor = white ? Color.white : new Color(0.10f, 0.10f, 0.12f);
    }

    private void SetFeedback(string text, Color color)
    {
        _feedback.text = text;
        _feedback.style.color = color;
        _feedback.style.backgroundColor = string.IsNullOrEmpty(text) ? new Color(0, 0, 0, 0) : new Color(color.r, color.g, color.b, 0.14f);
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

    private VisualElement Overlay()
    {
        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0, 0, 0, 0.72f);
        dim.style.alignItems = Align.Center; dim.style.justifyContent = Justify.Center;
        return dim;
    }

    private VisualElement OverlayPanel() => UiKit.OverlayCard(); // scrolling, height-capped modal card

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cap_warmup.png"));
        yield return new WaitForSeconds(0.6f);
        ShowIntro();
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cap_intro.png"));
        StartRun();
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cap_position.png"));
        if (_targets.Count > 0) { OnPick(_targets.First()); }
        yield return new WaitForSeconds(0.3f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cap_picked.png"));
        Submit();
        yield return new WaitForSeconds(1.2f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cap_next.png"));
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
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
