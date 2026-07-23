using System;
using System.Collections;
using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Puzzle Storm (Lichess model): a running clock, difficulty that ramps with each solve, and a combo
// that tops the clock up at every milestone; a miss costs time and breaks the combo but the run keeps
// going. Distinct from Puzzle Blitz's fixed clock and three-strikes. Scoring is the pure-core
// StormScoring; this scene drives the clock and the solve loop. No engine required.
public sealed class StormController : MonoBehaviour
{
    private const double StartSeconds = 180;
    private const double RampPerSolve = 25;

    private Board2D _board;
    private ScenarioLibrary _lib;
    private readonly GradeExtractor _grader = new();
    private StormScoring _storm;
    private Scenario _scenario;
    private double _target = 700;
    private string _from;
    private bool _running;

    private VisualElement _root, _overlayHost, _boardHost;
    private Label _time, _solved, _combo, _feedback;

    private static readonly Color Sel = new(0.36f, 0.72f, 0.42f, 0.55f);

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
        _root.Add(UiKit.NavRail("Puzzle Storm"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 4;
        _time = UiKit.Text_("3:00", 26, UiKit.Gold, bold: true);
        _solved = UiKit.Text_("0 solved", 16, UiKit.Text, bold: true);
        head.Add(_time); head.Add(_solved);
        center.Add(head);

        _combo = UiKit.Text_("", 13, UiKit.Dim, bold: true);
        _combo.style.marginBottom = 6;
        center.Add(_combo);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        _board = new Board2D(null);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        _boardHost.Add(_board.Root);
        center.Add(_boardHost);

        _feedback = UiKit.Text_("", 15, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 10; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 4, 12, 4, 12); UiKit.Radius(_feedback, 8);
        _feedback.style.whiteSpace = WhiteSpace.Normal; _feedback.style.maxWidth = 480;
        center.Add(_feedback);
        _root.Add(center);

        _board.Render(ChessGame.StartFen, canMove: false, lastMove: null, whiteBottom: true);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-stormtest"))
            StartCoroutine(AutoDemo());
        else
            ShowIntro();
    }

    private ScenarioLibrary Library() => _lib ??= ScenarioLibrary.LoadDefault();

    private void ShowIntro()
    {
        var dim = Overlay();
        var panel = UiKit.OverlayCard(); panel.style.width = 470;
        panel.Add(UiKit.Text_("Puzzle Storm", 28, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Solve as many as you can before the clock runs out. Each solve raises the difficulty; a combo of correct answers adds time. A miss costs time and resets the combo - but the run continues.", 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 10; sub.style.marginBottom = 18;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var start = UiKit.Primary("Start", StartRun, 16); start.style.width = 320;
        panel.Add(start);
        dim.Add(panel); _overlayHost.Add(dim);
    }

    private void StartRun()
    {
        KaissaStreak.RecordToday();
        _overlayHost.Clear();
        _storm = new StormScoring(StartSeconds);
        _target = 700;
        _from = null;
        _running = true;
        SetFeedback("", UiKit.Dim);
        NextPuzzle();
    }

    private void NextPuzzle()
    {
        if (!_running) return;
        var pool = Library().AllScenarios.OrderBy(s => Math.Abs(s.Rating - _target)).ThenBy(s => s.Id, StringComparer.Ordinal);
        _scenario = pool.First();
        _from = null;
        bool whiteBottom = SideWhite(_scenario.Fen);
        _board.SquareClickHandler = OnPick;
        _board.Render(_scenario.Fen, canMove: false, lastMove: null, whiteBottom: whiteBottom);
        RenderHud();
    }

    private void OnPick(string sq)
    {
        if (!_running || _scenario == null || string.IsNullOrEmpty(sq)) return;
        if (_from == null) { _from = sq; _board.HighlightSquare(sq, Sel); return; }
        if (sq == _from) { _from = null; _board.Render(_scenario.Fen, canMove: false, lastMove: null, whiteBottom: SideWhite(_scenario.Fen)); return; }

        var attempt = _grader.Grade(_scenario, _from + sq, TimeSpan.FromSeconds(3));
        _from = null;
        if (attempt.Correct)
        {
            _storm.OnSolve();
            _target += RampPerSolve;
            SetFeedback("Correct", UiKit.GreenHi);
        }
        else
        {
            _storm.OnMiss();
            var reveal = _scenario.Solutions.Count > 0 ? SanOf(_scenario.Fen, _scenario.Solutions[0]) : "?";
            SetFeedback($"Miss - best was {reveal}  (-10s)", UiKit.Danger);
        }
        if (_storm.IsOver) { EndRun(); return; }
        NextPuzzle();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        { SceneTransition.Go("Menu"); return; }

        if (!_running || _storm == null) return;
        _storm.Tick(Time.deltaTime);
        RenderHud();
        if (_storm.IsOver) EndRun();
    }

    private void RenderHud()
    {
        if (_storm == null) return;
        int secs = Math.Max(0, (int)Math.Ceiling(_storm.TimeRemaining));
        _time.text = $"{secs / 60}:{secs % 60:00}";
        _time.style.color = secs <= 10 ? UiKit.Danger : UiKit.Gold;
        _solved.text = $"{_storm.Solved} solved";
        _combo.text = _storm.Combo > 0 ? $"Combo {_storm.Combo}   (next bonus in {_storm.ToNextBonus})" : "Build a combo for bonus time";
    }

    private void EndRun()
    {
        if (!_running) return;
        _running = false;
        _board.SquareClickHandler = null;

        if (_storm.Solved > KaissaSettings.StormBest) KaissaSettings.StormBest = _storm.Solved;
        if (_storm.BestCombo > KaissaSettings.StormBestCombo) KaissaSettings.StormBestCombo = _storm.BestCombo;

        var dim = Overlay();
        var panel = UiKit.OverlayCard(); panel.style.width = 420;
        panel.Add(UiKit.Text_("Time!", 26, UiKit.Text, bold: true));
        var sub = UiKit.Text_($"Solved {_storm.Solved} with a best combo of {_storm.BestCombo}.\nBest ever: {KaissaSettings.StormBest} solved.", 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 8; sub.style.marginBottom = 16;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var again = UiKit.Primary("Play again", StartRun, 15); again.style.width = 300; again.style.marginBottom = 8;
        panel.Add(again);
        panel.Add(UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu"), 14));
        dim.Add(panel); _overlayHost.Add(dim);
    }

    private static bool SideWhite(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length < 2 || parts[1] == "w";
    }

    private static string SanOf(string fen, string uci)
    {
        try { return ChessGame.FromFen(fen).SanForUci(uci) ?? uci; }
        catch { return uci; }
    }

    private void SetFeedback(string text, Color color)
    {
        _feedback.text = text;
        _feedback.style.color = color;
        _feedback.style.backgroundColor = string.IsNullOrEmpty(text) ? new Color(0, 0, 0, 0) : new Color(color.r, color.g, color.b, 0.14f);
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

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        ShowIntro();
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "storm_intro.png"));
        StartRun();
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "storm_board.png"));
        yield return new WaitForSeconds(0.3f);
        Application.Quit();
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
