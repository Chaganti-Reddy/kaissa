using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

// Puzzles screen, rebuilt to mirror chess.com's puzzle-solving page and layered with Kaissa's own
// progression. Every puzzle is multi-move: the player finds each solver move and the opponent's
// replies play automatically (via the pure-C# PuzzleSession). Controls: Hint, Solution, Retry, Next,
// Analyze. Three feeds: Rated (adaptive/spaced, moves your rating), by Theme, and by Difficulty band
// (the last two are unrated "custom" practice, like chess.com). Works on the 2D or 3D board through
// IBoardView; the correct/incorrect badge is a board-agnostic overlay so both boards get it.
public sealed class KaissaBoardController : MonoBehaviour
{
    private enum Mode { Rated, Theme, Difficulty, Daily, Weakness, Misses }

    private KaissaTrainer _trainer;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;
    private readonly System.Random _rng = new();

    private Mode _mode = Mode.Rated;
    private PuzzleSession _session;
    private TrainingCard _card;        // rated feed only (for grading)
    private Scenario _scenario;        // custom/daily feed
    private int _puzzleRating;
    private IReadOnlyList<string> _themes = Array.Empty<string>();
    private string _patternName = "";
    private bool _whiteBottom = true;
    private bool _busy;                 // animating a reply; ignore input
    private bool _hintUsed;
    private int _hintStage;
    private bool _wrongThisPuzzle;
    private bool _graded;               // rating already applied for this puzzle
    private bool _solutionShown;
    private bool _concluded;            // solved or given up; awaiting Next
    private float _elapsed;
    private bool _timing;

    private List<Scenario> _feed = new();
    private int _feedAt;

    private int _answered, _correct, _solveStreak;
    private double _ratingStart;
    private bool _summaryShown;

    private VisualElement _root;
    private VisualElement _sideBadge, _sideDot;
    private Label _sideText;
    private Label _puzzleRatingLabel, _playerRatingLabel, _streakLabel, _solvedLabel, _timerLabel;
    private Label _feedbackLabel, _tierLabel, _xpLabel, _modeLabel;
    private VisualElement _themeChips, _xpFill, _masteryBody, _pickerHost;
    private Button _hintBtn, _solutionBtn, _retryBtn, _nextBtn, _analyzeBtn;

    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.55f);
    private static readonly Color Bad = new(0.92f, 0.33f, 0.33f, 0.55f);
    private static readonly Color HintCol = new(0.51f, 0.72f, 0.30f, 0.60f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
        KaissaPractice.FoldInto(_trainer);
        _ratingStart = _trainer.PlayerRating;

        _audio = PieceAudio.Attach(gameObject);

        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        StartCoroutine(BuildUi(doc));
    }

    private IEnumerator BuildUi(UIDocument doc)
    {
        yield return null;
        _root = doc.rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Row;
        _root.style.flexGrow = 1;
        _root.style.backgroundColor = UiKit.Bg;

        _root.Add(UiKit.NavRail("SampleScene"));
        _root.Add(BuildCenter());
        _root.Add(BuildRightRail());

        _board = BoardMount.Create(gameObject, _boardHost, _root, uci => OnPlayerMove(uci), _audio);

        if (DailyRoute.Active) { DailyRoute.Active = false; _mode = Mode.Daily; }
        else if (!string.IsNullOrEmpty(ThemeRoute.PatternId))
        {
            _mode = Mode.Theme;
            _patternName = string.IsNullOrEmpty(ThemeRoute.PatternName) ? ThemeRoute.PatternId : ThemeRoute.PatternName;
            LoadThemeFeed(ThemeRoute.PatternId);
            ThemeRoute.PatternId = null; ThemeRoute.PatternName = null;
        }

        RefreshProgression();
        LoadNext();

        if (Environment.GetCommandLineArgs().Contains("-kaissa-puzzletest"))
            StartCoroutine(AutoDemo());
    }

    // Self-test driver: exercises every control and interaction on the Puzzles page and films the
    // motion densely, so the whole page can be verified frame-by-frame without a human (2D and 3D).
    // Covers: setup arrival, Hint (both stages), a wrong move (snap-back + red flash + reveal), a full
    // multi-move solve (glide + green flash + reply), Next, Solution (played out), Retry, the Themes and
    // Difficulty pickers, and board flip.
    private System.Collections.IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        string tag = KaissaSettings.BoardView == 1 ? "3d" : "2d";
        KaissaSettings.AutoQueen = true; // never let a promotion picker block a scripted solve

        // Warm-up (first capture of a session is unreliable), then the loaded position.
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_warmup.png"));
        yield return new WaitForSeconds(0.6f);
        yield return Burst(dir, $"pz_{tag}_setup", 8, 0.05f);

        // Hint via a real click on the Hint button: first press highlights the piece, second reveals it.
        UiAutomation.Click(_hintBtn);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_hint1.png"));
        UiAutomation.Click(_hintBtn);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_hint2.png"));
        yield return new WaitForSeconds(0.3f);

        // Wrong move through the real board input path: snap-back, red flash, revealed square.
        PlayMove(FindLegalNonSolution());
        yield return Burst(dir, $"pz_{tag}_wrong", 14, 0.05f);

        // Solve the whole line through the board input path: glide, correct flash, opponent reply.
        int guard = 0;
        while (!_concluded && _session != null && _session.ExpectedMove is { } mv && guard++ < 20)
        {
            PlayMove(mv);
            yield return Burst(dir, $"pz_{tag}_solve{guard}", 6, 0.05f);
        }
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_solved.png"));

        UiAutomation.Click(_nextBtn);
        yield return new WaitForSeconds(1.0f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_next.png"));

        // Solution button: play out the whole line.
        UiAutomation.Click(_solutionBtn);
        yield return Burst(dir, $"pz_{tag}_solution", 16, 0.06f);

        // Retry: fresh puzzle (Next), a wrong move, then the Retry button resets to the start.
        UiAutomation.Click(_nextBtn);
        yield return new WaitForSeconds(0.8f);
        PlayMove(FindLegalNonSolution());
        yield return new WaitForSeconds(0.8f);
        UiAutomation.Click(_retryBtn);
        yield return new WaitForSeconds(0.7f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_retry.png"));

        UiAutomation.Click(UiAutomation.FindButton(_root, "Themes"));
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_themes.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(_pickerHost.Q("pickrow"));
        yield return new WaitForSeconds(0.9f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_theme_loaded.png"));
        yield return new WaitForSeconds(0.3f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Difficulty"));
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_difficulty.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(_pickerHost.Q("pickrow"));
        yield return new WaitForSeconds(0.9f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_difficulty_loaded.png"));
        yield return new WaitForSeconds(0.3f);

        // Weakness: one click generates a tailored set and loads it (no picker).
        UiAutomation.Click(UiAutomation.FindButton(_root, "Weakness"));
        yield return new WaitForSeconds(0.9f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_weakness.png"));
        yield return new WaitForSeconds(0.3f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Rated"));
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_rated.png"));

        // Board flip (F key on this page - no on-page button).
        Flip();
        yield return new WaitForSeconds(0.7f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_flip.png"));
        yield return new WaitForSeconds(0.4f);

        // Session summary overlay + its buttons (Esc-triggered UI; drive its buttons by real click).
        ShowSummary();
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_summary.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Keep training"));
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_summary_dismissed.png"));
        yield return new WaitForSeconds(0.3f);

        // Analyze routes to the Analysis scene - do this last, then quit.
        UiAutomation.Click(_analyzeBtn);
        yield return new WaitForSeconds(1.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"pz_{tag}_analyze.png"));
        yield return new WaitForSeconds(0.4f);
        Application.Quit();
    }

    // Play a move the way a click does: through the board's real input path (falls back to the move
    // callback only for promotions, whose picker AutoQueen bypasses anyway).
    private void PlayMove(string uci)
    {
        if (string.IsNullOrEmpty(uci)) return;
        if (uci.Length == 4) _board.DebugClickMove(uci.Substring(0, 2), uci.Substring(2, 2));
        else OnPlayerMove(uci);
    }

    private System.Collections.IEnumerator Burst(string dir, string prefix, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{prefix}_{i:000}.png"));
            yield return new WaitForSeconds(interval);
        }
    }

    private string FindLegalNonSolution()
    {
        if (_session == null) return null;
        var expected = _session.ExpectedMove;
        foreach (var mv in ChessGame.FromFen(_session.Fen).LegalUciMoves())
            if (!string.Equals(mv, expected, StringComparison.OrdinalIgnoreCase))
                return mv;
        return null;
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key) return args[i + 1];
        return null;
    }

    private VisualElement BuildCenter()
    {
        var center = new VisualElement();
        center.style.flexGrow = 1;
        center.style.alignItems = Align.Center;
        UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 10;
        head.Add(UiKit.Text_("Puzzles", 24, UiKit.Text, bold: true));
        head.Add(BuildModeBar());
        center.Add(head);

        var sideRow = UiKit.Row();
        sideRow.style.width = 480; sideRow.style.justifyContent = Justify.SpaceBetween; sideRow.style.marginBottom = 8;
        _sideBadge = MakeSideBadge();
        sideRow.Add(_sideBadge);
        _puzzleRatingLabel = UiKit.Text_("", 15, UiKit.Dim, bold: true);
        sideRow.Add(_puzzleRatingLabel);
        center.Add(sideRow);

        var boardWrap = new VisualElement();
        boardWrap.style.width = 480; boardWrap.style.height = 480; boardWrap.style.flexShrink = 0;
        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.position = Position.Absolute;
        boardWrap.Add(_boardHost);
        center.Add(boardWrap);

        _feedbackLabel = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedbackLabel.style.marginTop = 10; _feedbackLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedbackLabel, 6, 14, 6, 14); UiKit.Radius(_feedbackLabel, 8); // pill so it reads over the 3D board
        center.Add(_feedbackLabel);

        center.Add(BuildControls());
        return center;
    }

    private VisualElement BuildModeBar()
    {
        var bar = UiKit.Row();
        bar.Add(ModeChip("Rated", () => SwitchMode(Mode.Rated)));
        bar.Add(ModeChip("Themes", ToggleThemePicker));
        bar.Add(ModeChip("Difficulty", ToggleDifficultyPicker));
        bar.Add(ModeChip("Weakness", LoadWeaknessFeed));
        bar.Add(ModeChip("Review misses", LoadMissesFeed));
        _modeLabel = UiKit.Text_("", 12, UiKit.Mute);
        _modeLabel.style.marginLeft = 8;
        bar.Add(_modeLabel);
        return bar;
    }

    private Button ModeChip(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 12);
        b.style.marginLeft = 6;
        UiKit.Pad(b, 6, 10, 6, 10);
        return b;
    }

    private VisualElement MakeSideBadge()
    {
        _sideDot = new VisualElement();
        _sideDot.style.width = 12; _sideDot.style.height = 12; UiKit.Radius(_sideDot, 6);
        _sideDot.style.marginRight = 8;
        _sideDot.style.borderTopWidth = _sideDot.style.borderBottomWidth = _sideDot.style.borderLeftWidth = _sideDot.style.borderRightWidth = 1;
        _sideDot.style.borderTopColor = _sideDot.style.borderBottomColor = _sideDot.style.borderLeftColor = _sideDot.style.borderRightColor = UiKit.Line;
        _sideText = UiKit.Text_("", 16, UiKit.Text, bold: true);
        var row = UiKit.Row(_sideDot, _sideText);
        UiKit.Pad(row, 6, 12, 6, 12); UiKit.Radius(row, 6);
        return row;
    }

    private VisualElement BuildControls()
    {
        var row = UiKit.Row();
        row.style.marginTop = 12; row.style.justifyContent = Justify.Center;
        _hintBtn = CtrlBtn("Hint", ShowHint);
        _solutionBtn = CtrlBtn("Solution", ShowSolution);
        _retryBtn = CtrlBtn("Retry", Retry);
        _nextBtn = CtrlBtn("Next", () => LoadNext());
        _analyzeBtn = CtrlBtn("Analyze", Analyze);
        row.Add(_hintBtn); row.Add(_solutionBtn); row.Add(_retryBtn); row.Add(_nextBtn); row.Add(_analyzeBtn);
        return row;
    }

    private Button CtrlBtn(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 14);
        b.style.marginLeft = 5; b.style.marginRight = 5; b.style.minWidth = 88;
        return b;
    }

    private VisualElement BuildRightRail()
    {
        var rail = new VisualElement();
        rail.style.width = 340;
        UiKit.Pad(rail, 18, 24, 18, 8);

        var prog = Panel();
        UiKit.Pad(prog, 14, 16, 14, 16);
        _tierLabel = UiKit.Text_("", 18, UiKit.Gold, bold: true);
        prog.Add(_tierLabel);
        var track = new VisualElement();
        track.style.height = 10; track.style.marginTop = 8; track.style.marginBottom = 6;
        track.style.backgroundColor = UiKit.Panel3; UiKit.Radius(track, 5);
        _xpFill = new VisualElement();
        _xpFill.style.height = 10; UiKit.Radius(_xpFill, 5); _xpFill.style.backgroundColor = UiKit.Gold;
        track.Add(_xpFill);
        prog.Add(track);
        _xpLabel = UiKit.Text_("", 12, UiKit.Dim);
        prog.Add(_xpLabel);
        var chips = UiKit.Row();
        chips.style.marginTop = 10;
        _streakLabel = UiKit.Text_("", 13, UiKit.Text, bold: true);
        _solvedLabel = UiKit.Text_("", 13, UiKit.Text, bold: true);
        _timerLabel = UiKit.Text_("", 13, UiKit.Dim, bold: true);
        chips.Add(Pill(_streakLabel)); chips.Add(Pill(_solvedLabel)); chips.Add(Pill(_timerLabel));
        prog.Add(chips);
        rail.Add(prog);

        var rate = Panel(); rate.style.marginTop = 14; UiKit.Pad(rate, 14, 16, 14, 16);
        _playerRatingLabel = UiKit.Text_("", 15, UiKit.Text, bold: true);
        rate.Add(_playerRatingLabel);
        rail.Add(rate);

        var themes = Panel(); themes.style.marginTop = 14; UiKit.Pad(themes, 12, 14, 12, 14);
        themes.Add(UiKit.Text_("Themes", 12, UiKit.Mute, bold: true));
        _themeChips = new VisualElement();
        _themeChips.style.flexDirection = FlexDirection.Row; _themeChips.style.flexWrap = Wrap.Wrap; _themeChips.style.marginTop = 6;
        themes.Add(_themeChips);
        rail.Add(themes);

        var mastery = Panel(); mastery.style.marginTop = 14; UiKit.Pad(mastery, 12, 14, 12, 14);
        var mh = UiKit.Text_("Pattern mastery", 12, UiKit.Mute, bold: true);
        mastery.Add(mh);
        var scroll = UiKit.Scroll(); scroll.style.maxHeight = 190; scroll.style.marginTop = 6;
        _masteryBody = scroll.contentContainer;
        mastery.Add(scroll);
        rail.Add(mastery);
        return rail;
    }

    private static VisualElement Pill(Label content)
    {
        var p = UiKit.Row(content);
        p.style.backgroundColor = UiKit.Panel3; UiKit.Radius(p, 14); UiKit.Pad(p, 5, 10, 5, 10);
        p.style.marginRight = 6;
        return p;
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

    private void SwitchMode(Mode m, string patternId = null, string label = null)
    {
        HidePicker();
        _mode = m;
        if (m == Mode.Theme && patternId != null) { _patternName = label ?? patternId; LoadThemeFeed(patternId); }
        _summaryShown = false;
        LoadNext();
    }

    private void LoadThemeFeed(string patternId)
    {
        var list = _trainer.Library.ForPattern(new PatternId(patternId)).ToList();
        Shuffle(list);
        _feed = list; _feedAt = 0;
    }

    private void LoadDifficultyFeed(int lo, int hi, string label)
    {
        _mode = Mode.Difficulty; _patternName = label;
        var list = _trainer.Library.ByRatingRange(lo, hi).ToList();
        Shuffle(list);
        _feed = list; _feedAt = 0;
        _summaryShown = false;
        LoadNext();
    }

    // Auto-generated practice tailored to the player: a mixed set drawn from the patterns their skill
    // model marks weakest (least stable memory / most lapses). Falls back to a band around their rating
    // when nothing has been seen yet (a brand-new player has no weaknesses to target).
    private void LoadWeaknessFeed()
    {
        if (_pickerHost != null) HidePicker();
        var saved = KaissaProgress.Load();
        var model = saved != null ? SkillModel.FromJson(saved) : new SkillModel();
        var list = WeaknessReport.BuildPracticeSet(model, _trainer.Library, patternCount: 4, perPattern: 4).ToList();
        if (list.Count == 0)
        {
            int r = (int)_trainer.PlayerRating;
            list = _trainer.Library.ByRatingRange(r - 150, r + 150).Take(16).ToList();
            _patternName = "Tailored to your level";
        }
        else _patternName = "Your weak spots";

        _mode = Mode.Weakness;
        Shuffle(list);
        _feed = list; _feedAt = 0;
        _summaryShown = false;
        LoadNext();
    }

    // Replay the puzzles the player got wrong or gave up on (persisted in KaissaMisses).
    private void LoadMissesFeed()
    {
        if (_pickerHost != null) HidePicker();
        var list = KaissaMisses.AsScenarios();
        if (list.Count == 0)
        {
            SetFeedback("No missed puzzles yet - they collect here when you slip up.", UiKit.Dim);
            return;
        }
        _mode = Mode.Misses; _patternName = "Your misses";
        _feed = list; _feedAt = 0;
        _summaryShown = false;
        LoadNext();
    }

    // Persist the current puzzle as a miss (wrong move or gave up), for the Review-misses feed.
    private void RecordMiss()
    {
        if (_mode == Mode.Misses) return; // don't re-record while reviewing misses
        string fen = _session?.StartFen;
        var line = _card?.Line ?? _scenario?.SolverLine;
        string setup = _card?.Setup ?? _scenario?.Setup;
        var themes = _card?.Themes ?? _scenario?.ThemeTags;
        string pattern = _card?.PatternName ?? _scenario?.Pattern.Value;
        if (fen != null && line != null) KaissaMisses.Record(fen, line, setup, _puzzleRating, themes, pattern);
    }

    private void Shuffle(List<Scenario> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void LoadNext()
    {
        _card = null; _scenario = null;

        switch (_mode)
        {
            case Mode.Rated:
                _card = _trainer.NextCard();
                if (_card == null) { SetFeedback("No puzzles available.", UiKit.Dim); return; }
                _session = new PuzzleSession(_card.Board.Fen, _card.Line, _card.Setup);
                _puzzleRating = _card.PuzzleRating; _themes = _card.Themes; _patternName = _card.PatternName;
                _modeLabel.text = "Rated (adaptive)";
                break;

            case Mode.Daily:
                _scenario = DailyPuzzle.ForDate(_trainer.Library, DateTime.Today);
                _session = new PuzzleSession(_scenario);
                _puzzleRating = _scenario.Rating; _themes = _scenario.ThemeTags; _patternName = "Daily puzzle";
                _modeLabel.text = "Daily";
                break;

            default: // Theme / Difficulty custom feeds
                if (_feed.Count == 0) { SetFeedback("No puzzles in this filter.", UiKit.Dim); return; }
                _scenario = _feed[_feedAt % _feed.Count]; _feedAt++;
                _session = new PuzzleSession(_scenario);
                _puzzleRating = _scenario.Rating; _themes = _scenario.ThemeTags;
                _modeLabel.text = $"{_patternName} (unrated)";
                break;
        }

        _hintUsed = false; _hintStage = 0; _wrongThisPuzzle = false; _graded = false;
        _solutionShown = false; _concluded = false; _busy = false;
        _elapsed = 0f; _timing = true;
        SetFeedback("", UiKit.Dim);
        // Always orient to the solver's side (their colour at the bottom), like a chess.com puzzle
        // and like Play. StartFen is already the solver-to-move position; the setup move only led
        // into it and is not applied on top. The Flip control still lets the player flip by hand.
        _whiteBottom = IsWhiteToMove(_session.StartFen);

        UpdateSideBadge();
        UpdateThemeChips();
        UpdateInfoLabels();
        SetControlsSolving();

        // Render with the opponent's setup move animating in, chess.com-style.
        _board.Render(_session.StartFen, canMove: true, lastMove: _session.SetupMove, whiteBottom: _whiteBottom);
    }

    private void OnPlayerMove(string uci)
    {
        if (_busy || _concluded || _summaryShown || _session == null) return;

        var result = _session.Submit(uci);

        if (result.Outcome == PuzzleOutcome.Wrong)
        {
            _wrongThisPuzzle = true;
            RecordMiss();
            _board.Render(_session.Fen, canMove: true, lastMove: null, whiteBottom: _whiteBottom); // snap back
            TintMove(uci, Bad);
            _audio.PlayWrong();
            SetFeedback("Not the move - try again.", UiKit.Danger);
            if (_mode == Mode.Rated && !_graded) GradeRated(correct: false, playedMove: uci);
            return;
        }

        TintMove(result.PlayerMove, Good);

        if (result.Outcome == PuzzleOutcome.Continue)
        {
            _audio.PlayCorrect();
            _busy = true;
            SetFeedback("Best move - keep going.", UiKit.GreenHi);
            StartCoroutine(PlayReply(result));
        }
        else // Solved (final correct move: the Victory cue + flourish stand in for the move cue)
        {
            _board.Render(result.FenAfterReply, canMove: false, lastMove: result.ReplyMove, whiteBottom: _whiteBottom);
            OnSolved();
        }
    }

    private IEnumerator PlayReply(PuzzleMoveResult result)
    {
        yield return new WaitForSeconds(0.28f);
        _board.Render(result.FenAfterReply, canMove: true, lastMove: result.ReplyMove, whiteBottom: _whiteBottom);
        UpdateSideBadge();
        _busy = false;
    }

    private void OnSolved()
    {
        _audio.PlayVictory();
        BoardCelebrate.Burst(_boardHost);
        _timing = false; _concluded = true;
        // A wrong attempt in Rated mode already counted this puzzle via GradeRated; don't count it twice.
        if (!_graded) _answered++;
        _correct++;
        _solveStreak++;
        if (_solveStreak > KaissaSettings.PuzzleBestStreak) KaissaSettings.PuzzleBestStreak = _solveStreak;
        KaissaStreak.RecordToday();

        int xp = 0;
        if (!_solutionShown)
        {
            bool firstTry = !_wrongThisPuzzle && !_hintUsed;
            xp = PuzzleProgression.XpForSolve(_puzzleRating, _trainer.PlayerRating, _hintUsed, firstTry, _elapsed, _solveStreak);
            KaissaSettings.PuzzleXp += xp;
        }

        string tail = xp > 0 ? $"  +{xp} XP" : "";
        if (_mode == Mode.Rated && !_graded)
        {
            var before = _trainer.PlayerRating;
            GradeRated(correct: true, playedMove: _card.Line[0]);
            int delta = Mathf.RoundToInt((float)(_trainer.PlayerRating - before));
            SetFeedback($"Solved!  Rating {delta:+0;-0}{tail}", UiKit.GreenHi);
        }
        else
        {
            SetFeedback($"Solved!{tail}", UiKit.GreenHi);
        }

        RefreshProgression();
        UpdateInfoLabels();
        SetControlsConcluded();
        StartCoroutine(AutoAdvance());
    }

    private IEnumerator AutoAdvance()
    {
        yield return new WaitForSeconds(2.2f);
        if (_concluded && !_summaryShown) LoadNext();
    }

    private void GradeRated(bool correct, string playedMove)
    {
        _graded = true;
        if (!correct) { _answered++; _solveStreak = 0; }
        // trainer grades the move against the card's accepted solution; assisted = hint used.
        _trainer.Answer(correct ? _card.Line[0] : playedMove, TimeSpan.FromSeconds(_elapsed), _hintUsed);
        KaissaProgress.Save(_trainer.ExportProgress());
    }

    private void ShowHint()
    {
        if (_concluded || _session == null || _session.ExpectedMove == null) return;
        _hintUsed = true;
        var mv = _session.ExpectedMove;
        _board.HighlightSquare(mv.Substring(0, 2), HintCol);
        if (_hintStage >= 1) _board.HighlightSquare(mv.Substring(2, 2), HintCol); // second press reveals the move
        _hintStage++;
        SetFeedback(_hintStage >= 2 ? "There's the move." : "Look at the highlighted piece.", UiKit.Dim);
    }

    private void ShowSolution()
    {
        if (_session == null || _concluded) { if (_concluded) LoadNext(); return; }
        _solutionShown = true;
        RecordMiss();
        _timing = false;
        if (_mode == Mode.Rated && !_graded) GradeRated(correct: false, playedMove: _session.ExpectedMove ?? "0000");
        StartCoroutine(PlayOutSolution());
    }

    private IEnumerator PlayOutSolution()
    {
        _busy = true;
        while (_session.ExpectedMove is { } mv)
        {
            var r = _session.Submit(mv);
            TintMove(r.PlayerMove, Good);
            _board.Render(r.FenAfterPlayer, canMove: false, lastMove: r.PlayerMove, whiteBottom: _whiteBottom);
            yield return new WaitForSeconds(0.5f);
            if (r.ReplyMove != null)
            {
                _board.Render(r.FenAfterReply, canMove: false, lastMove: r.ReplyMove, whiteBottom: _whiteBottom);
                yield return new WaitForSeconds(0.45f);
            }
        }
        _busy = false; _concluded = true;
        SetFeedback("Solution shown.", UiKit.Dim);
        SetControlsConcluded();
    }

    private void Retry()
    {
        if (_session == null) return;
        // Replay the same puzzle for practice. Rating is not re-applied if already graded.
        _session = _scenario != null ? new PuzzleSession(_scenario) : new PuzzleSession(_card.Board.Fen, _card.Line, _card.Setup);
        _hintUsed = false; _hintStage = 0; _wrongThisPuzzle = false; _solutionShown = false;
        _concluded = false; _busy = false; _elapsed = 0f; _timing = true;
        SetFeedback("", UiKit.Dim);
        // Always orient to the solver's side; StartFen is already the solver-to-move position.
        _whiteBottom = IsWhiteToMove(_session.StartFen);
        UpdateSideBadge();
        SetControlsSolving();
        _board.Render(_session.StartFen, canMove: true, lastMove: _session.SetupMove, whiteBottom: _whiteBottom);
    }

    private void Analyze()
    {
        if (_session == null) return;
        AnalysisRoute.Fen = _session.Fen;
        SceneTransition.Go("Analysis");
    }

    private void SetControlsSolving()
    {
        Enable(_hintBtn, true); Enable(_solutionBtn, true); Enable(_retryBtn, true);
        Enable(_nextBtn, false); Enable(_analyzeBtn, true);
    }

    private void SetControlsConcluded()
    {
        Enable(_hintBtn, false); Enable(_solutionBtn, false); Enable(_retryBtn, true);
        Enable(_nextBtn, true); Enable(_analyzeBtn, true);
    }

    private static void Enable(Button b, bool on)
    {
        if (b == null) return;
        b.SetEnabled(on);
        b.style.opacity = on ? 1f : 0.45f;
    }

    private void ToggleThemePicker()
    {
        if (_pickerHost != null) { HidePicker(); return; }
        var (panel, body) = PickerPanel("Choose a theme");
        foreach (var pid in _trainer.Library.Patterns)
        {
            var p = _trainer.Library.Describe(pid);
            body.Add(PickerRow(p.Name, () => SwitchMode(Mode.Theme, pid.Value, p.Name)));
        }
        ShowPicker(panel);
    }

    private void ToggleDifficultyPicker()
    {
        if (_pickerHost != null) { HidePicker(); return; }
        var (panel, body) = PickerPanel("Choose a difficulty");
        body.Add(PickerRow("Beginner  (400-800)", () => { HidePicker(); LoadDifficultyFeed(400, 800, "Beginner"); }));
        body.Add(PickerRow("Easy  (800-1200)", () => { HidePicker(); LoadDifficultyFeed(800, 1200, "Easy"); }));
        body.Add(PickerRow("Medium  (1200-1600)", () => { HidePicker(); LoadDifficultyFeed(1200, 1600, "Medium"); }));
        body.Add(PickerRow("Hard  (1600-2000)", () => { HidePicker(); LoadDifficultyFeed(1600, 2000, "Hard"); }));
        body.Add(PickerRow("Expert  (2000-2400)", () => { HidePicker(); LoadDifficultyFeed(2000, 2400, "Expert"); }));
        body.Add(PickerRow("Master  (2400-2800)", () => { HidePicker(); LoadDifficultyFeed(2400, 2800, "Master"); }));
        body.Add(PickerRow("Grandmaster  (2800+)", () => { HidePicker(); LoadDifficultyFeed(2800, 3300, "Grandmaster"); }));
        ShowPicker(panel);
    }

    // Returns the outer panel (for ShowPicker) and an inner scrollable body to add rows to, so a long
    // list (e.g. all themes) scrolls inside a bounded modal instead of overflowing the screen.
    private (VisualElement panel, VisualElement body) PickerPanel(string title)
    {
        var panel = Panel();
        panel.style.width = 320; UiKit.Pad(panel, 10, 12, 10, 12); panel.style.maxHeight = 620;
        var h = UiKit.Text_(title, 13, UiKit.Mute, bold: true); h.style.marginBottom = 6;
        panel.Add(h);
        var scroll = UiKit.Scroll(); scroll.style.maxHeight = 560;
        panel.Add(scroll);
        return (panel, scroll.contentContainer);
    }

    private VisualElement PickerRow(string label, Action onClick)
    {
        var row = UiKit.Row(UiKit.Text_(label, 14, UiKit.Text));
        row.name = "pickrow";
        UiKit.Pad(row, 8, 10, 8, 10); UiKit.Radius(row, 6);
        row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = UiKit.Panel2);
        row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = new Color(0, 0, 0, 0));
        row.RegisterCallback<ClickEvent>(_ => onClick());
        UiKit.Interactive(row, 1.02f);
        return row;
    }

    // A centered modal overlay (dim + panel), so the picker never overlaps the right-rail panels.
    private void ShowPicker(VisualElement panel)
    {
        HidePicker();
        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0, 0, 0, 0.6f);
        dim.style.alignItems = Align.Center; dim.style.justifyContent = Justify.Center;
        dim.RegisterCallback<ClickEvent>(e => { if (e.target == dim) HidePicker(); }); // click backdrop to close
        dim.Add(panel);
        _pickerHost = dim;
        _root.Add(dim);
    }

    private void HidePicker()
    {
        if (_pickerHost != null && _pickerHost.parent != null) _pickerHost.parent.Remove(_pickerHost);
        _pickerHost = null;
    }

    private void UpdateSideBadge()
    {
        bool white = IsWhiteToMove(_session.Fen);
        _sideText.text = white ? "White to move" : "Black to move";
        _sideDot.style.backgroundColor = white ? Color.white : new Color(0.10f, 0.10f, 0.12f);
        _sideBadge.style.backgroundColor = white ? new Color(1, 1, 1, 0.12f) : new Color(0, 0, 0, 0.28f);
    }

    private void UpdateThemeChips()
    {
        _themeChips.Clear();
        foreach (var t in _themes.Take(8))
            _themeChips.Add(ThemeChip(Prettify(t)));
        if (_themes.Count == 0)
            _themeChips.Add(UiKit.Text_("-", 12, UiKit.Mute));
    }

    private VisualElement ThemeChip(string label)
    {
        var c = UiKit.Row(UiKit.Text_(label, 12, UiKit.Text, bold: true));
        c.style.backgroundColor = UiKit.Panel3; UiKit.Radius(c, 12); UiKit.Pad(c, 4, 9, 4, 9);
        c.style.marginRight = 5; c.style.marginBottom = 5;
        return c;
    }

    private void UpdateInfoLabels()
    {
        _puzzleRatingLabel.text = $"Puzzle {_puzzleRating}";
        _playerRatingLabel.text = $"Your rating  {_trainer.PlayerRating:0}";
        _streakLabel.text = $"{KaissaStreak.CurrentDays()}d streak";
        _solvedLabel.text = $"Solved {_correct}/{_answered}";
    }

    private void RefreshProgression()
    {
        var st = PuzzleProgression.Standing(KaissaSettings.PuzzleXp);
        _tierLabel.text = st.Name;
        _xpFill.style.width = new Length(st.Fraction * 100f, LengthUnit.Percent);
        _xpLabel.text = st.IsMax
            ? $"{st.TotalXp:n0} XP (max tier)"
            : $"{st.XpIntoTier:n0} / {st.XpForNext:n0} XP to {PuzzleProgression.Tiers[st.Index + 1].Name}";

        _masteryBody.Clear();
        foreach (var row in _trainer.Progress())
        {
            var m = PuzzleProgression.MasteryFor(row);
            var r = UiKit.Row();
            r.style.justifyContent = Justify.SpaceBetween; r.style.marginBottom = 3;
            r.Add(UiKit.Text_(row.PatternName, 12, UiKit.Dim));
            var lvl = UiKit.Text_(PuzzleProgression.MasteryLabel(m), 12, MasteryColor(m), bold: true);
            r.Add(lvl);
            _masteryBody.Add(r);
        }
    }

    private static Color MasteryColor(PuzzleProgression.Mastery m) => m switch
    {
        PuzzleProgression.Mastery.Mastered => UiKit.Gold,
        PuzzleProgression.Mastery.Strong => UiKit.GreenHi,
        PuzzleProgression.Mastery.Proficient => UiKit.Green,
        PuzzleProgression.Mastery.Familiar => UiKit.Dim,
        PuzzleProgression.Mastery.Learning => UiKit.Mute,
        _ => UiKit.Mute,
    };

    private void SetFeedback(string text, Color color)
    {
        _feedbackLabel.text = text;
        _feedbackLabel.style.color = color;
        _feedbackLabel.style.backgroundColor = string.IsNullOrEmpty(text) ? new Color(0, 0, 0, 0) : new Color(0, 0, 0, 0.55f);
    }

    private void TintMove(string uci, Color c)
    {
        if (string.IsNullOrEmpty(uci) || uci.Length < 4) return;
        _board.HighlightSquare(uci.Substring(0, 2), c);
        _board.HighlightSquare(uci.Substring(2, 2), c);
    }

    private static bool IsWhiteToMove(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length < 2 || parts[1] != "b";
    }

    private static string Prettify(string theme)
    {
        if (string.IsNullOrEmpty(theme)) return theme;
        var sb = new System.Text.StringBuilder();
        sb.Append(char.ToUpperInvariant(theme[0]));
        for (int i = 1; i < theme.Length; i++)
        {
            if (char.IsUpper(theme[i])) sb.Append(' ');
            sb.Append(theme[i]);
        }
        return sb.ToString();
    }

    private void Update()
    {
        if (_timing) _elapsed += Time.deltaTime;
        if (_timerLabel != null && _timing)
            _timerLabel.text = $"{_elapsed:0.0}s";

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (_answered > 0 && !_summaryShown) ShowSummary();
            else SceneTransition.Go("Menu");
        }
        else if (kb.hKey.wasPressedThisFrame) ShowHint();
        else if (kb.nKey.wasPressedThisFrame && _concluded) LoadNext();
        else if (kb.rKey.wasPressedThisFrame) Retry();
        else if (kb.fKey.wasPressedThisFrame) Flip();
    }

    private void Flip()
    {
        if (_session == null) return;
        _whiteBottom = !_whiteBottom;
        _board.Render(_session.Fen, !_concluded && !_busy, null, _whiteBottom);
        UpdateSideBadge();
    }

    private void ShowSummary()
    {
        _summaryShown = true;
        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0, 0, 0, 0.78f);
        dim.style.alignItems = Align.Center; dim.style.justifyContent = Justify.Center;

        var panel = Panel();
        UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center;
        int pct = _answered > 0 ? Mathf.RoundToInt(100f * _correct / _answered) : 0;
        int delta = Mathf.RoundToInt((float)(_trainer.PlayerRating - _ratingStart));
        var st = PuzzleProgression.Standing(KaissaSettings.PuzzleXp);
        panel.Add(UiKit.Text_("Session summary", 26, UiKit.Text, bold: true));
        var s1 = UiKit.Text_($"Solved {_correct}/{_answered}   ({pct}%)", 17, UiKit.Dim); s1.style.marginTop = 10; panel.Add(s1);
        var s2 = UiKit.Text_($"Rating {_ratingStart:0} -> {_trainer.PlayerRating:0}   ({delta:+0;-0})", 17, UiKit.Dim);
        s2.style.marginTop = 4; panel.Add(s2);
        var s3 = UiKit.Text_($"{st.Name}  -  {st.TotalXp:n0} XP  -  best streak {KaissaSettings.PuzzleBestStreak}", 15, UiKit.Gold);
        s3.style.marginTop = 4; s3.style.marginBottom = 18; panel.Add(s3);
        var keep = UiKit.Primary("Keep training", () => { _root.Remove(dim); _summaryShown = false; }, 15);
        keep.style.width = 300; keep.style.marginBottom = 8; panel.Add(keep);
        var back = UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu")); back.style.width = 300; panel.Add(back);
        dim.Add(panel);
        _root.Add(dim);
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
