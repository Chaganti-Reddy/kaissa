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

// Board Vision drill: a square name is shown; say whether it is a light or dark square, as many as you
// can in 30 seconds. Trains instant square-colour recognition. Correct/incorrect feedback reveals the
// real colour; per-run best score is kept. Keyboard: L = light, D = dark.
public sealed class VisionController : MonoBehaviour
{
    private const float RunSeconds = 30f;

    private VisionSession _session;
    private string _current = "";
    private bool _running, _busy;
    private float _timeLeft;

    private Label _prompt, _timerLabel, _scoreLabel, _bestLabel, _feedback;
    private Button _lightBtn, _darkBtn;
    private VisualElement _root, _overlayHost;

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }
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
        _root.Add(UiKit.NavRail("Vision"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; center.style.justifyContent = Justify.Center;
        UiKit.Pad(center, 18, 24, 18, 24);

        _timerLabel = UiKit.Text_("", 24, UiKit.Gold, bold: true);
        center.Add(_timerLabel);
        center.Add(UiKit.Text_("Light or dark square?", 20, UiKit.Dim, bold: true));
        _prompt = UiKit.Text_("", 84, UiKit.Text, bold: true);
        _prompt.style.marginTop = 6; _prompt.style.marginBottom = 20;
        center.Add(_prompt);

        var row = UiKit.Row();
        _lightBtn = UiKit.Primary("Light", () => Answer(true), 18); _lightBtn.style.width = 170; _lightBtn.style.marginRight = 10;
        _darkBtn = UiKit.Ghost("Dark", () => Answer(false), 18); _darkBtn.style.width = 170;
        row.Add(_lightBtn); row.Add(_darkBtn);
        center.Add(row);

        _feedback = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 18; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 4, 12, 4, 12); UiKit.Radius(_feedback, 8);
        center.Add(_feedback);
        _root.Add(center);

        var rail = new VisualElement();
        rail.style.width = 300; UiKit.Pad(rail, 18, 24, 18, 8);
        var panel = Panel(); UiKit.Pad(panel, 16);
        panel.Add(UiKit.Text_("SCORE", 11, UiKit.Mute, bold: true));
        _scoreLabel = UiKit.Text_("0", 40, UiKit.Text, bold: true);
        panel.Add(_scoreLabel);
        _bestLabel = UiKit.Text_($"Best {KaissaSettings.VisionBest}", 13, UiKit.Dim, bold: true);
        panel.Add(_bestLabel);
        rail.Add(panel);
        _root.Add(rail);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-visiontest"))
            StartCoroutine(AutoDemo());
        else
            ShowStartOverlay();
    }

    private void ShowStartOverlay()
    {
        _running = false;
        _timerLabel.text = ""; _prompt.text = ""; SetFeedback("", UiKit.Dim);
        var dim = Overlay();
        var panel = Panel(); UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center; panel.style.width = 420;
        panel.Add(UiKit.Text_("Board Vision", 28, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Is the square light or dark? Answer as many as you can in 30 seconds. Keys: L / D.", 14, UiKit.Dim);
        sub.style.marginTop = 8; sub.style.marginBottom = 18; sub.style.whiteSpace = WhiteSpace.Normal; sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var start = UiKit.Primary("Start", StartRun, 16); start.style.width = 320;
        panel.Add(start);
        dim.Add(panel);
        _overlayHost.Add(dim);
    }

    private void StartRun()
    {
        _overlayHost.Clear();
        _session = new VisionSession();
        _timeLeft = RunSeconds; _running = true; _busy = false;
        _scoreLabel.text = "0"; _timerLabel.text = Fmt(RunSeconds);
        SetFeedback("", UiKit.Dim);
        Next();
    }

    private void Answer(bool guessLight)
    {
        if (!_running || _busy) return;
        bool correct = _session.Answer(guessLight);
        _scoreLabel.text = _session.Score.ToString();
        if (correct)
        {
            SetFeedback("Correct", UiKit.GreenHi);
            Next();
        }
        else
        {
            _busy = true;
            bool actuallyLight = BoardVision.IsLightSquare(_current);
            SetFeedback($"{_current} is {(actuallyLight ? "light" : "dark")}", UiKit.Danger);
            StartCoroutine(ResumeAfter(0.8f));
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
        _current = _session.NextSquare();
        _prompt.text = _current;
    }

    private void EndRun()
    {
        _running = false;
        int score = _session?.Score ?? 0;
        bool record = score > KaissaSettings.VisionBest;
        if (record) KaissaSettings.VisionBest = score;
        _bestLabel.text = $"Best {KaissaSettings.VisionBest}";
        _timerLabel.text = "0:00"; _prompt.text = "";

        var dim = Overlay();
        var panel = Panel(); UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center; panel.style.width = 420;
        panel.Add(UiKit.Text_("Time", 15, UiKit.Dim, bold: true));
        panel.Add(UiKit.Text_(score.ToString(), 56, UiKit.Text, bold: true));
        panel.Add(UiKit.Text_("correct", 13, UiKit.Mute));
        if (record) { var b = UiKit.Text_("New best", 15, UiKit.Gold, bold: true); b.style.marginTop = 8; panel.Add(b); }
        var again = UiKit.Primary("Play again", StartRun, 15); again.style.width = 320; again.style.marginTop = 16; again.style.marginBottom = 8;
        panel.Add(again);
        var menu = UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu")); menu.style.width = 320;
        panel.Add(menu);
        dim.Add(panel);
        _overlayHost.Add(dim);
    }

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
            if (_timeLeft <= 0f) { EndRun(); return; }
            var kb = Keyboard.current;
            if (kb != null && !_busy)
            {
                if (kb.lKey.wasPressedThisFrame) Answer(true);
                else if (kb.dKey.wasPressedThisFrame) Answer(false);
            }
        }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneTransition.Go("Menu");
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);

        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vision_warmup.png"));
        yield return new WaitForSeconds(0.6f);
        ShowStartOverlay();
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vision_start.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Start"));
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vision_playing.png"));
        yield return new WaitForSeconds(0.4f);

        // Correct answer (light iff the square is light), then a deliberate wrong one, via real button clicks.
        bool isLight = BoardVision.IsLightSquare(_current);
        UiAutomation.Click(isLight ? _lightBtn : _darkBtn);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vision_correct.png"));
        yield return new WaitForSeconds(0.4f);
        bool isLight2 = BoardVision.IsLightSquare(_current);
        UiAutomation.Click(isLight2 ? _darkBtn : _lightBtn); // wrong on purpose
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vision_wrong.png"));
        yield return new WaitForSeconds(0.6f);

        _timeLeft = 0.1f;
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vision_gameover.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Play again"));
        yield return new WaitForSeconds(0.7f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vision_again.png"));
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
