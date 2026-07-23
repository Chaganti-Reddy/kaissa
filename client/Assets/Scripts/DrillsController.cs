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

// Drills: the named training modes (Time Trainer, Intuition, Defender, Advantage Capitalization,
// Blunder Preventer, Checkmate Patterns, Opening Improver), each generated from the shared puzzle
// content by the pure core (DrillFactory). Most are solve-on-the-board; Blunder Preventer is a
// two-choice "pick the stronger move". Grading reuses GradeExtractor. No engine required.
public sealed class DrillsController : MonoBehaviour
{
    private const int DrillLength = 10;

    private Board2D _board;
    private ScenarioLibrary _lib;
    private readonly GradeExtractor _grader = new();

    private DrillKind _kind;
    private IReadOnlyList<Scenario> _scenarios;
    private IReadOnlyList<TwoChoiceProblem> _twoChoice;
    private int _index;
    private int _score;
    private string _from;

    private VisualElement _root, _overlayHost, _boardHost, _choiceHost;
    private Label _title, _score_, _prompt, _feedback;

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
        _root.Add(UiKit.NavRail("Drills"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 6;
        _title = UiKit.Text_("Drills", 18, UiKit.Text, bold: true);
        _score_ = UiKit.Text_("", 18, UiKit.Gold, bold: true);
        head.Add(_title); head.Add(_score_);
        center.Add(head);

        _prompt = UiKit.Text_("", 13, UiKit.Dim, bold: true);
        _prompt.style.marginBottom = 6; _prompt.style.whiteSpace = WhiteSpace.Normal; _prompt.style.maxWidth = 480;
        center.Add(_prompt);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        _board = new Board2D(null);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        _boardHost.Add(_board.Root);
        center.Add(_boardHost);

        _choiceHost = UiKit.Row(); _choiceHost.style.marginTop = 12; _choiceHost.style.justifyContent = Justify.Center;
        center.Add(_choiceHost);

        _feedback = UiKit.Text_("", 15, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 8; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 4, 12, 4, 12); UiKit.Radius(_feedback, 8);
        _feedback.style.whiteSpace = WhiteSpace.Normal; _feedback.style.maxWidth = 480;
        center.Add(_feedback);

        var back = UiKit.Row(); back.style.marginTop = 10;
        back.Add(UiKit.Ghost("Drill menu", ShowMenu, 14));
        center.Add(back);
        _root.Add(center);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        _board.Render(ChessGame.StartFen, canMove: false, lastMove: null, whiteBottom: true);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-drilltest"))
            StartCoroutine(AutoDemo());
        else
            ShowMenu();
    }

    private ScenarioLibrary Library() => _lib ??= ScenarioLibrary.LoadDefault();

    private void ShowMenu()
    {
        var dim = Overlay();
        var panel = UiKit.OverlayCard(); panel.style.width = 520;
        panel.Add(UiKit.Text_("Drills", 26, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Targeted training modes built from your puzzle content. Pick one to begin.", 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 8; sub.style.marginBottom = 14;
        panel.Add(sub);

        foreach (DrillKind kind in Enum.GetValues(typeof(DrillKind)))
        {
            var k = kind;
            var card = new VisualElement();
            card.style.backgroundColor = UiKit.Panel2; UiKit.Radius(card, 10); UiKit.Pad(card, 10, 14, 10, 14);
            card.style.marginBottom = 8;
            UiKit.Interactive(card);
            card.Add(UiKit.Text_(DrillFactory.TitleOf(k), 16, UiKit.Text, bold: true));
            var d = UiKit.Text_(DrillFactory.DescriptionOf(k), 13, UiKit.Dim);
            d.style.whiteSpace = WhiteSpace.Normal; d.style.marginTop = 2;
            card.Add(d);
            card.RegisterCallback<ClickEvent>(_ => { _overlayHost.Clear(); StartDrill(k); });
            panel.Add(card);
        }
        dim.Add(panel); _overlayHost.Add(dim);
    }

    private void StartDrill(DrillKind kind)
    {
        KaissaStreak.RecordToday();
        _kind = kind;
        _index = 0; _score = 0; _from = null;
        _title.text = DrillFactory.TitleOf(kind);
        SetFeedback("", UiKit.Dim);

        if (kind == DrillKind.BlunderPreventer)
        {
            _twoChoice = DrillFactory.BlunderPreventer(Library(), DrillLength);
            _scenarios = null;
            ShowTwoChoice();
        }
        else
        {
            _scenarios = DrillFactory.Build(kind, Library(), DrillLength).Scenarios;
            _twoChoice = null;
            ShowSolve();
        }
    }

    // -- board-solve drills -------------------------------------------------

    private void ShowSolve()
    {
        _choiceHost.Clear();
        if (_scenarios == null || _index >= _scenarios.Count) { EndDrill(); return; }
        var sc = _scenarios[_index];
        _prompt.text = sc.Prompt;
        _score_.text = $"{_score} / {_scenarios.Count}";
        bool whiteBottom = SideWhite(sc.Fen);
        _from = null;
        _board.SquareClickHandler = OnPick;
        _board.Render(sc.Fen, canMove: false, lastMove: null, whiteBottom: whiteBottom);
    }

    private void OnPick(string sq)
    {
        if (_scenarios == null || _index >= _scenarios.Count || string.IsNullOrEmpty(sq)) return;
        if (_from == null) { _from = sq; _board.HighlightSquare(sq, Sel); return; }
        if (sq == _from) { _from = null; ShowSolve(); return; }

        var sc = _scenarios[_index];
        var attempt = _grader.Grade(sc, _from + sq, TimeSpan.FromSeconds(5));
        _from = null;
        if (attempt.Correct) _score++;
        var reveal = sc.Solutions.Count > 0 ? SanOf(sc.Fen, sc.Solutions[0]) : "?";
        SetFeedback(attempt.Correct ? $"Correct - {reveal}" : $"Best was {reveal}",
            attempt.Correct ? UiKit.GreenHi : UiKit.Danger);
        _board.SquareClickHandler = null;
        StartCoroutine(AdvanceSolve());
    }

    private IEnumerator AdvanceSolve()
    {
        yield return new WaitForSeconds(0.7f);
        _index++;
        ShowSolve();
    }

    // -- two-choice (Blunder Preventer) -------------------------------------

    private void ShowTwoChoice()
    {
        if (_twoChoice == null || _index >= _twoChoice.Count) { EndDrill(); return; }
        var p = _twoChoice[_index];
        _prompt.text = "Pick the stronger move.";
        _score_.text = $"{_score} / {_twoChoice.Count}";
        _board.SquareClickHandler = null;
        _board.Render(p.Fen, canMove: false, lastMove: null, whiteBottom: SideWhite(p.Fen));

        _choiceHost.Clear();
        foreach (var uci in p.Options)
        {
            var u = uci;
            var btn = UiKit.Ghost(SanOf(p.Fen, u), () => PickChoice(u), 15);
            btn.style.width = 150; btn.style.marginLeft = 6; btn.style.marginRight = 6;
            _choiceHost.Add(btn);
        }
    }

    private void PickChoice(string uci)
    {
        if (_twoChoice == null || _index >= _twoChoice.Count) return;
        var p = _twoChoice[_index];
        bool correct = uci == p.BetterUci;
        if (correct) _score++;
        SetFeedback(correct ? "Correct - the stronger move." : $"Stronger was {SanOf(p.Fen, p.BetterUci)}.",
            correct ? UiKit.GreenHi : UiKit.Danger);
        _choiceHost.Clear();
        StartCoroutine(AdvanceChoice());
    }

    private IEnumerator AdvanceChoice()
    {
        yield return new WaitForSeconds(0.7f);
        _index++;
        ShowTwoChoice();
    }

    // -- end ----------------------------------------------------------------

    private void EndDrill()
    {
        _choiceHost.Clear();
        int total = _twoChoice?.Count ?? _scenarios?.Count ?? 0;
        var dim = Overlay();
        var panel = UiKit.OverlayCard(); panel.style.width = 420;
        panel.Add(UiKit.Text_("Drill complete", 24, UiKit.Text, bold: true));
        var sub = UiKit.Text_($"You scored {_score} of {total} in {DrillFactory.TitleOf(_kind)}.", 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 8; sub.style.marginBottom = 16;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var again = UiKit.Primary("Another drill", () => { _overlayHost.Clear(); ShowMenu(); }, 15);
        again.style.width = 300; again.style.marginBottom = 8;
        panel.Add(again);
        panel.Add(UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu"), 14));
        dim.Add(panel); _overlayHost.Add(dim);
    }

    // -- helpers ------------------------------------------------------------

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

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneTransition.Go("Menu");
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        ShowMenu();
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "drills_menu.png"));
        _overlayHost.Clear(); StartDrill(DrillKind.CheckmatePatterns);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "drills_solve.png"));
        _overlayHost.Clear(); StartDrill(DrillKind.BlunderPreventer);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "drills_twochoice.png"));
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
