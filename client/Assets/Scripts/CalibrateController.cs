using System;
using System.Collections;
using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// First-run calibration: a short adaptive run of puzzles that estimates a starting rating and seeds it,
// so difficulty fits from the very first session. Intro screen, a progress bar and side-to-move badge,
// green/red feedback per answer, and a rating reveal with a level descriptor at the end. 2D or 3D board.
public sealed class CalibrateController : MonoBehaviour
{
    private const int Puzzles = 12;

    private CalibrationSession _session;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;
    private Scenario _scenario;
    private bool _running, _busy, _whiteBottom = true;

    private VisualElement _root, _overlayHost, _sideBadge, _sideDot, _progressFill;
    private Label _sideText, _progressLabel, _feedback;

    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.55f);
    private static readonly Color Bad = new(0.92f, 0.33f, 0.33f, 0.55f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }
        _audio = PieceAudio.Attach(gameObject);
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
        _root.Add(UiKit.NavRail("Calibrate"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var track = new VisualElement();
        track.style.width = 480; track.style.height = 8; track.style.backgroundColor = UiKit.Panel3; UiKit.Radius(track, 4);
        _progressFill = new VisualElement();
        _progressFill.style.height = 8; UiKit.Radius(_progressFill, 4); _progressFill.style.backgroundColor = UiKit.Gold; _progressFill.style.width = 0;
        track.Add(_progressFill);
        center.Add(track);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginTop = 8; head.style.marginBottom = 8;
        _sideBadge = MakeSideBadge();
        head.Add(_sideBadge);
        _progressLabel = UiKit.Text_("", 14, UiKit.Dim, bold: true);
        head.Add(_progressLabel);
        center.Add(head);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        center.Add(_boardHost);

        _feedback = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 10; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 4, 12, 4, 12); UiKit.Radius(_feedback, 8);
        center.Add(_feedback);
        _root.Add(center);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        _board = BoardMount.Create(gameObject, _boardHost, _root, uci => OnPlayerMove(uci), _audio);
        _board.Render(ChessGame.StartFen, canMove: false, lastMove: null, whiteBottom: true);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-calibratetest"))
            StartCoroutine(AutoDemo());
        else
            ShowIntro();
    }

    private void ShowIntro()
    {
        _running = false;
        var dim = Overlay();
        var panel = Panel(); UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center; panel.style.width = 460;
        panel.Add(UiKit.Text_("Find your level", 28, UiKit.Text, bold: true));
        var sub = UiKit.Text_($"Solve {Puzzles} quick puzzles. They adapt to your answers to set a starting rating, so training fits you from the first session. Just play your move on the board.", 14, UiKit.Dim);
        sub.style.marginTop = 10; sub.style.marginBottom = 18; sub.style.whiteSpace = WhiteSpace.Normal; sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var start = UiKit.Primary("Start", StartRun, 16); start.style.width = 320;
        panel.Add(start);
        dim.Add(panel);
        _overlayHost.Add(dim);
    }

    private void StartRun()
    {
        _overlayHost.Clear();
        _session = new CalibrationSession(ScenarioLibrary.LoadDefault(), puzzles: Puzzles);
        _running = true; _busy = false;
        SetFeedback("", UiKit.Dim);
        DealNext();
    }

    private void DealNext()
    {
        if (_session.IsComplete) { Finish(); return; }
        _scenario = _session.Next();
        if (_scenario == null) { Finish(); return; }
        var view = BoardView.FromFen(_scenario.Fen);
        _whiteBottom = view.WhiteToMove;
        _board.Render(_scenario.Fen, canMove: true, lastMove: null, whiteBottom: _whiteBottom);
        UpdateSideBadge(view.WhiteToMove);
        _progressLabel.text = $"Puzzle {_session.Answered + 1} of {_session.Total}";
        _progressFill.style.width = new Length((float)_session.Answered / _session.Total * 100f, LengthUnit.Percent);
        SetFeedback("", UiKit.Dim);
        _busy = false;
    }

    private void OnPlayerMove(string uci)
    {
        if (!_running || _busy || _scenario == null) return;
        _busy = true;

        bool correct = SolutionMatches(_scenario, uci);
        var after = ApplyMove(_scenario.Fen, uci);
        if (after != null) _board.Render(after, canMove: false, lastMove: uci, whiteBottom: _whiteBottom);
        TintMove(uci, correct ? Good : Bad);
        if (correct) { _audio.PlayCorrect(); SetFeedback("Correct", UiKit.GreenHi); }
        else { _audio.PlayWrong(); SetFeedback("Not the move - keep going", UiKit.Danger); }

        _session.Submit(uci, TimeSpan.FromSeconds(4));
        StartCoroutine(AdvanceAfter(0.7f));
    }

    private IEnumerator AdvanceAfter(float s)
    {
        yield return new WaitForSeconds(s);
        _progressFill.style.width = new Length((float)_session.Answered / _session.Total * 100f, LengthUnit.Percent);
        DealNext();
    }

    private void Finish()
    {
        _running = false;
        _audio.PlayVictory();
        int rating = (int)Math.Round(_session.EstimatedRating);
        var saved = KaissaProgress.Load();
        var model = saved != null ? SkillModel.FromJson(saved) : new SkillModel();
        model.RatingEstimate = rating;
        KaissaProgress.Save(model.ToJson());
        KaissaSettings.Onboarded = true;

        _board.Render("8/8/8/8/8/8/8/8 w - - 0 1", canMove: false, lastMove: null, whiteBottom: true);
        _progressFill.style.width = Length.Percent(100);
        _progressLabel.text = "Done"; SetFeedback("", UiKit.Dim);

        var dim = Overlay();
        var panel = Panel(); UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center; panel.style.width = 460;
        panel.Add(UiKit.Text_("Your starting rating", 15, UiKit.Dim, bold: true));
        panel.Add(UiKit.Text_(rating.ToString(), 60, UiKit.Text, bold: true));
        var tier = UiKit.Text_(Descriptor(rating), 16, UiKit.Gold, bold: true); tier.style.marginBottom = 16;
        panel.Add(tier);
        var go = UiKit.Primary("Start training", () => SceneTransition.Go("SampleScene"), 15); go.style.width = 320; go.style.marginBottom = 8;
        panel.Add(go);
        var again = UiKit.Ghost("Recalibrate", ShowIntro); again.style.width = 320; again.style.marginBottom = 8;
        panel.Add(again);
        var menu = UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu")); menu.style.width = 320;
        panel.Add(menu);
        dim.Add(panel);
        _overlayHost.Add(dim);
    }

    private static string Descriptor(int rating) => rating switch
    {
        < 800 => "Beginner",
        < 1200 => "Casual",
        < 1600 => "Intermediate",
        < 2000 => "Advanced",
        _ => "Expert",
    };

    private VisualElement MakeSideBadge()
    {
        _sideDot = new VisualElement();
        _sideDot.style.width = 12; _sideDot.style.height = 12; UiKit.Radius(_sideDot, 6); _sideDot.style.marginRight = 8;
        _sideDot.style.borderTopWidth = _sideDot.style.borderBottomWidth = _sideDot.style.borderLeftWidth = _sideDot.style.borderRightWidth = 1;
        _sideDot.style.borderTopColor = _sideDot.style.borderBottomColor = _sideDot.style.borderLeftColor = _sideDot.style.borderRightColor = UiKit.Line;
        _sideText = UiKit.Text_("", 15, UiKit.Text, bold: true);
        var row = UiKit.Row(_sideDot, _sideText);
        UiKit.Pad(row, 6, 12, 6, 12); UiKit.Radius(row, 6);
        return row;
    }

    private void UpdateSideBadge(bool white)
    {
        _sideText.text = white ? "White to move" : "Black to move";
        _sideDot.style.backgroundColor = white ? Color.white : new Color(0.10f, 0.10f, 0.12f);
        _sideBadge.style.backgroundColor = white ? new Color(1, 1, 1, 0.12f) : new Color(0, 0, 0, 0.28f);
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

    private void TintMove(string uci, Color c)
    {
        if (string.IsNullOrEmpty(uci) || uci.Length < 4) return;
        _board.HighlightSquare(uci.Substring(0, 2), c);
        _board.HighlightSquare(uci.Substring(2, 2), c);
    }

    private static bool SolutionMatches(Scenario s, string uci)
    {
        try
        {
            var g = ChessGame.FromFen(s.Fen);
            var played = g.ResolveToUci(uci) ?? uci;
            return s.Solutions.Any(sol => string.Equals(g.ResolveToUci(sol) ?? sol, played, StringComparison.OrdinalIgnoreCase));
        }
        catch { return s.Solutions.Any(sol => string.Equals(sol, uci, StringComparison.OrdinalIgnoreCase)); }
    }

    private static string ApplyMove(string fen, string uci)
    {
        try { var g = ChessGame.FromFen(fen); if (g.TryMakeMove(uci)) return g.Fen; }
        catch { }
        return null;
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
        string tag = KaissaSettings.BoardView == 1 ? "3d" : "2d";
        KaissaSettings.AutoQueen = true;

        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"cal_{tag}_warmup.png"));
        yield return new WaitForSeconds(0.8f);
        ShowIntro();
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"cal_{tag}_intro.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Start"));
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"cal_{tag}_puzzle.png"));
        yield return new WaitForSeconds(0.4f);

        int guard = 0;
        while (_running && _scenario != null && guard++ < Puzzles + 4)
        {
            var sol = _scenario.Solutions.Count > 0 ? _scenario.Solutions[0] : null;
            if (sol is { Length: >= 4 }) _board.DebugClickMove(sol.Substring(0, 2), sol.Substring(2, 2));
            yield return new WaitForSeconds(0.9f);
            if (guard == 2) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"cal_{tag}_feedback.png"));
        }
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"cal_{tag}_result.png"));
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
