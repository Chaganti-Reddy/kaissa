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

// Puzzle Blitz (chess.com Puzzle Rush): solve as many puzzles as you can under pressure. Three modes -
// 3 minutes, 5 minutes, or Survival (no clock). Three wrong moves end the run in every mode; the clock
// running out ends the timed modes. Difficulty ramps with each solve (RushSession). Puzzles are
// multi-move (PuzzleSession) and a wrong move at any step is a strike. No hints, no takebacks - that is
// the point. Per-mode personal bests are kept. Works on the 2D or 3D board through IBoardView.
public sealed class RushController : MonoBehaviour
{
    private enum RushMode { ThreeMin, FiveMin, Survival }

    private ScenarioLibrary _library;
    private RushSession _rush;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;

    private RushMode _mode = RushMode.ThreeMin;
    private float _timeLimit;      // seconds; 0 = survival (count up)
    private float _timeLeft;
    private float _elapsed;
    private bool _timing;

    private PuzzleSession _session;
    private Scenario _scenario;
    private bool _started, _over, _busy, _whiteBottom = true;
    private bool _graded;          // one strike/solve recorded per puzzle

    private VisualElement _root, _overlayHost;
    private VisualElement _sideBadge, _sideDot;
    private Label _sideText, _timerLabel, _scoreLabel, _streakLabel, _feedbackLabel, _bestLabel;
    private readonly VisualElement[] _strikeMarks = new VisualElement[3];

    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.55f);
    private static readonly Color Bad = new(0.92f, 0.33f, 0.33f, 0.55f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _audio = PieceAudio.Attach(gameObject);
        _library = ScenarioLibrary.LoadDefault();

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

        _root.Add(UiKit.NavRail("Rush"));
        _root.Add(BuildCenter());
        _root.Add(BuildRightRail());

        _board = BoardMount.Create(gameObject, _boardHost, _root, uci => OnPlayerMove(uci), _audio);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-rushtest"))
            StartCoroutine(AutoDemo());
        else
            ShowStartOverlay();
    }

    // ---------------- layout ----------------

    private VisualElement BuildCenter()
    {
        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center;
        UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 10;
        head.Add(UiKit.Text_("Puzzle Blitz", 24, UiKit.Text, bold: true));
        _timerLabel = UiKit.Text_("", 22, UiKit.Gold, bold: true);
        head.Add(_timerLabel);
        center.Add(head);

        var sideRow = UiKit.Row();
        sideRow.style.width = 480; sideRow.style.justifyContent = Justify.SpaceBetween; sideRow.style.marginBottom = 8;
        _sideBadge = MakeSideBadge();
        sideRow.Add(_sideBadge);
        sideRow.Add(BuildStrikes());
        center.Add(sideRow);

        var boardWrap = new VisualElement();
        boardWrap.style.width = 480; boardWrap.style.height = 480; boardWrap.style.flexShrink = 0;
        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.position = Position.Absolute;
        boardWrap.Add(_boardHost);
        center.Add(boardWrap);

        _feedbackLabel = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedbackLabel.style.marginTop = 10; _feedbackLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedbackLabel, 6, 14, 6, 14); UiKit.Radius(_feedbackLabel, 8);
        center.Add(_feedbackLabel);
        return center;
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

    private VisualElement BuildStrikes()
    {
        var row = UiKit.Row();
        for (int i = 0; i < 3; i++)
        {
            var mark = new VisualElement();
            mark.style.width = 16; mark.style.height = 16; UiKit.Radius(mark, 3);
            mark.style.marginLeft = 6;
            mark.style.borderTopWidth = mark.style.borderBottomWidth = mark.style.borderLeftWidth = mark.style.borderRightWidth = 2;
            mark.style.borderTopColor = mark.style.borderBottomColor = mark.style.borderLeftColor = mark.style.borderRightColor = UiKit.Mute;
            _strikeMarks[i] = mark;
            row.Add(mark);
        }
        return row;
    }

    private VisualElement BuildRightRail()
    {
        var rail = new VisualElement();
        rail.style.width = 340; UiKit.Pad(rail, 18, 24, 18, 8);

        var panel = Panel(); UiKit.Pad(panel, 16);
        panel.Add(UiKit.Text_("SCORE", 11, UiKit.Mute, bold: true));
        _scoreLabel = UiKit.Text_("0", 40, UiKit.Text, bold: true);
        panel.Add(_scoreLabel);
        _streakLabel = UiKit.Text_("", 13, UiKit.Dim, bold: true);
        panel.Add(_streakLabel);
        rail.Add(panel);

        var best = Panel(); best.style.marginTop = 14; UiKit.Pad(best, 14, 16, 14, 16);
        best.Add(UiKit.Text_("Personal best", 12, UiKit.Mute, bold: true));
        _bestLabel = UiKit.Text_("", 14, UiKit.Text, bold: true);
        _bestLabel.style.marginTop = 4; _bestLabel.style.whiteSpace = WhiteSpace.Normal;
        best.Add(_bestLabel);
        rail.Add(best);
        return rail;
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

    // ---------------- run lifecycle ----------------

    private void ShowStartOverlay()
    {
        _started = false; _over = false; _timing = false;
        _timerLabel.text = ""; SetFeedback("", UiKit.Dim);
        UpdateBestLabel();

        var dim = Overlay();
        var panel = Panel(); UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center; panel.style.width = 420;
        panel.Add(UiKit.Text_("Puzzle Blitz", 28, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Solve as many as you can. Three misses ends the run.", 14, UiKit.Dim);
        sub.style.marginTop = 8; sub.style.marginBottom = 18; sub.style.whiteSpace = WhiteSpace.Normal; sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);

        panel.Add(ModeButton("3 Minutes", () => StartRun(RushMode.ThreeMin)));
        panel.Add(ModeButton("5 Minutes", () => StartRun(RushMode.FiveMin)));
        panel.Add(ModeButton("Survival", () => StartRun(RushMode.Survival)));

        dim.Add(panel);
        _overlayHost.Add(dim);
        _overlayHost.style.display = DisplayStyle.Flex;
    }

    private VisualElement ModeButton(string label, Action onClick)
    {
        var btn = UiKit.Primary(label, onClick, 16);
        btn.style.width = 320; btn.style.marginBottom = 10;
        return btn;
    }

    private void StartRun(RushMode mode)
    {
        _mode = mode;
        _timeLimit = mode switch { RushMode.ThreeMin => 180f, RushMode.FiveMin => 300f, _ => 0f };
        _timeLeft = _timeLimit; _elapsed = 0f;
        _rush = new RushSession(_library, startRating: 600, lives: 3);
        _started = true; _over = false; _timing = true;
        _timerLabel.text = Fmt(_timeLimit > 0f ? _timeLimit : 0f); // paint the clock immediately, not next frame
        _overlayHost.style.display = DisplayStyle.None;
        _overlayHost.Clear();
        UpdateHud();
        DealNext();
    }

    private void DealNext()
    {
        _scenario = _rush.Next();
        if (_scenario == null) { EndRun("Out of puzzles"); return; }
        _session = new PuzzleSession(_scenario);
        _graded = false; _busy = false;
        SetFeedback("", UiKit.Dim);
        _whiteBottom = !KaissaSettings.Flip || IsWhiteToMove(_session.StartFen);
        UpdateSideBadge();
        _board.Render(_session.StartFen, canMove: true, lastMove: _session.SetupMove, whiteBottom: _whiteBottom);
    }

    private void OnPlayerMove(string uci)
    {
        if (!_started || _over || _busy || _session == null) return;

        var result = _session.Submit(uci);

        if (result.Outcome == PuzzleOutcome.Wrong)
        {
            RecordStrike(uci);
            return;
        }

        _audio.PlayCorrect();
        TintMove(result.PlayerMove, Good);

        if (result.Outcome == PuzzleOutcome.Continue)
        {
            _busy = true;
            StartCoroutine(PlayReply(result));
        }
        else
        {
            _board.Render(result.FenAfterReply, canMove: false, lastMove: result.ReplyMove, whiteBottom: _whiteBottom);
            RecordSolve();
        }
    }

    private IEnumerator PlayReply(PuzzleMoveResult result)
    {
        yield return new WaitForSeconds(0.22f);
        _board.Render(result.FenAfterReply, canMove: true, lastMove: result.ReplyMove, whiteBottom: _whiteBottom);
        UpdateSideBadge();
        _busy = false;
    }

    private void RecordSolve()
    {
        if (_graded) return;
        _graded = true;
        _rush.Submit(_scenario.Solutions[0], TimeSpan.Zero);
        UpdateHud();
        StartCoroutine(NextAfter(0.45f));
    }

    private void RecordStrike(string wrongMove)
    {
        if (_graded)
            return;
        _graded = true;
        _rush.Submit(wrongMove, TimeSpan.Zero); // grades as a miss -> costs a life
        _board.Render(_session.Fen, canMove: false, lastMove: null, whiteBottom: _whiteBottom); // snap back
        TintMove(wrongMove, Bad);
        var sol = _session.ExpectedMove;
        if (!string.IsNullOrEmpty(sol)) TintMove(sol, Good); // show the move that was there
        _audio.PlayWrong();
        SetFeedback("Strike!", UiKit.Danger);
        UpdateHud();
        if (_rush.IsOver) { StartCoroutine(EndAfter(1.0f, "Three strikes")); return; }
        StartCoroutine(NextAfter(1.0f));
    }

    private IEnumerator NextAfter(float seconds)
    {
        _busy = true;
        yield return new WaitForSeconds(seconds);
        _busy = false;
        if (!_over) DealNext();
    }

    private IEnumerator EndAfter(float seconds, string reason)
    {
        _busy = true;
        yield return new WaitForSeconds(seconds);
        EndRun(reason);
    }

    private void EndRun(string reason)
    {
        if (_over) return;
        _over = true; _timing = false; _started = false;

        int score = _rush.Score;
        bool record = SaveBest(score);
        if (record) _audio.PlayVictory(); else _audio.PlayGameEnd();

        var dim = Overlay();
        var panel = Panel(); UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center; panel.style.width = 420;
        panel.Add(UiKit.Text_(reason, 16, UiKit.Dim, bold: true));
        panel.Add(UiKit.Text_(score.ToString(), 56, UiKit.Text, bold: true));
        panel.Add(UiKit.Text_("puzzles solved", 13, UiKit.Mute));
        if (record)
        {
            var badge = UiKit.Text_("New personal best", 15, UiKit.Gold, bold: true);
            badge.style.marginTop = 8; panel.Add(badge);
        }
        else
        {
            var b = UiKit.Text_($"Best {BestFor(_mode)}", 14, UiKit.Dim); b.style.marginTop = 8; panel.Add(b);
        }

        var again = UiKit.Primary("Play again", () => StartRun(_mode), 15);
        again.style.width = 320; again.style.marginTop = 18; again.style.marginBottom = 8; panel.Add(again);
        var change = UiKit.Ghost("Change mode", ShowStartOverlay); change.style.width = 320; change.style.marginBottom = 8; panel.Add(change);
        var menu = UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu")); menu.style.width = 320; panel.Add(menu);

        dim.Add(panel);
        _overlayHost.Add(dim);
        _overlayHost.style.display = DisplayStyle.Flex;
    }

    // ---------------- hud / helpers ----------------

    private void UpdateHud()
    {
        if (_rush == null) return;
        _scoreLabel.text = _rush.Score.ToString();
        _streakLabel.text = _rush.Streak > 1 ? $"{_rush.Streak} in a row" : "";
        int strikes = Mathf.Clamp(3 - _rush.Lives, 0, 3);
        for (int i = 0; i < 3; i++)
        {
            bool hit = i < strikes;
            _strikeMarks[i].style.backgroundColor = hit ? UiKit.Danger : new Color(0, 0, 0, 0);
            var edge = hit ? UiKit.Danger : UiKit.Mute;
            _strikeMarks[i].style.borderTopColor = _strikeMarks[i].style.borderBottomColor =
                _strikeMarks[i].style.borderLeftColor = _strikeMarks[i].style.borderRightColor = edge;
        }
    }

    private void UpdateBestLabel()
    {
        _bestLabel.text = $"3 min: {KaissaSettings.RushBest3}\n5 min: {KaissaSettings.RushBest5}\nSurvival: {KaissaSettings.RushBestSurvival}";
    }

    private int BestFor(RushMode m) => m switch
    {
        RushMode.ThreeMin => KaissaSettings.RushBest3,
        RushMode.FiveMin => KaissaSettings.RushBest5,
        _ => KaissaSettings.RushBestSurvival,
    };

    private bool SaveBest(int score)
    {
        if (score <= BestFor(_mode)) return false;
        switch (_mode)
        {
            case RushMode.ThreeMin: KaissaSettings.RushBest3 = score; break;
            case RushMode.FiveMin: KaissaSettings.RushBest5 = score; break;
            default: KaissaSettings.RushBestSurvival = score; break;
        }
        return true;
    }

    private void UpdateSideBadge()
    {
        bool white = IsWhiteToMove(_session.Fen);
        _sideText.text = white ? "White to move" : "Black to move";
        _sideDot.style.backgroundColor = white ? Color.white : new Color(0.10f, 0.10f, 0.12f);
        _sideBadge.style.backgroundColor = white ? new Color(1, 1, 1, 0.12f) : new Color(0, 0, 0, 0.28f);
    }

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

    private VisualElement Overlay()
    {
        _overlayHost.Clear();
        var dim = new VisualElement();
        dim.style.flexGrow = 1;
        dim.style.backgroundColor = new Color(0, 0, 0, 0.80f);
        dim.style.alignItems = Align.Center; dim.style.justifyContent = Justify.Center;
        return dim;
    }

    private static bool IsWhiteToMove(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length < 2 || parts[1] != "b";
    }

    private static string Fmt(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    private void Update()
    {
        if (_timing)
        {
            if (_timeLimit > 0f)
            {
                _timeLeft -= Time.deltaTime;
                _timerLabel.text = Fmt(_timeLeft);
                if (_timeLeft <= 0f) { _timing = false; EndRun("Time's up"); }
            }
            else
            {
                _elapsed += Time.deltaTime;
                _timerLabel.text = Fmt(_elapsed);
            }
        }

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) SceneTransition.Go("Menu");
        else if (kb.fKey.wasPressedThisFrame && _session != null && !_busy)
        {
            _whiteBottom = !_whiteBottom;
            _board.Render(_session.Fen, !_over, null, _whiteBottom);
        }
    }

    // Self-test: films the whole flow densely so every frame of motion can be reviewed, not just
    // endpoints - the mode picker, the setup-move arrival, a solve (move glide + correct flash +
    // opponent reply), a wrong move (snap-back + strike flash + revealed solution), and the game-over
    // summary. Runs in 2D and 3D. Frames are a dense burst (~0.05s apart) so the animation is visible.
    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        string tag = KaissaSettings.BoardView == 1 ? "3d" : "2d";
        KaissaSettings.AutoQueen = true;

        // Warm-up capture: the very first ScreenCapture of a session is unreliable, so spend it on a
        // throwaway frame before the real ones.
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"rush_{tag}_warmup.png"));
        yield return new WaitForSeconds(0.4f);

        // Mode picker overlay.
        ShowStartOverlay();
        yield return new WaitForSeconds(1.2f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"rush_{tag}_a_modes.png"));
        yield return new WaitForSeconds(0.3f);

        // Start a run with a REAL click on the "3 Minutes" mode button, then film the setup move.
        UiAutomation.Click(UiAutomation.FindButton(_root, "3 Minutes"));
        yield return Burst(dir, $"rush_{tag}_setup", 10, 0.05f);

        // One solver move through the real board input path: glide, correct flash, opponent reply.
        PlayMove(_session?.ExpectedMove);
        yield return Burst(dir, $"rush_{tag}_solve", 16, 0.05f);
        yield return new WaitForSeconds(0.3f);

        // A wrong move: attempted piece snaps back, red strike flash, correct move revealed in green.
        PlayMove(FindLegalNonSolution());
        yield return Burst(dir, $"rush_{tag}_wrong", 18, 0.05f);

        // Two more misses to reach the game-over summary.
        yield return MissOnce();
        yield return MissOnce();
        yield return new WaitForSeconds(1.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"rush_{tag}_z_gameover.png"));
        yield return new WaitForSeconds(0.4f);

        // Game-over buttons: Play again (real click) restarts the same mode.
        UiAutomation.Click(UiAutomation.FindButton(_root, "Play again"));
        yield return new WaitForSeconds(1.0f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"rush_{tag}_playagain.png"));

        // Burn out this run, then Change mode (real click) back to the picker and start Survival.
        yield return MissOnce(); yield return MissOnce(); yield return MissOnce();
        yield return new WaitForSeconds(1.2f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Change mode"));
        yield return new WaitForSeconds(0.7f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"rush_{tag}_changemode.png"));
        yield return new WaitForSeconds(0.3f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Survival"));
        yield return new WaitForSeconds(1.0f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"rush_{tag}_survival.png"));

        // Flip (F key on this page - no on-page button).
        if (_session != null) { _whiteBottom = !_whiteBottom; _board.Render(_session.Fen, !_over, null, _whiteBottom); }
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"rush_{tag}_flip.png"));
        yield return new WaitForSeconds(0.4f);
        Application.Quit();
    }

    // Play a move through the board's real input path (falls back to the callback for promotions).
    private void PlayMove(string uci)
    {
        if (string.IsNullOrEmpty(uci)) return;
        if (uci.Length == 4) _board.DebugClickMove(uci.Substring(0, 2), uci.Substring(2, 2));
        else OnPlayerMove(uci);
    }

    private IEnumerator Burst(string dir, string prefix, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{prefix}_{i:000}.png"));
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator MissOnce()
    {
        int lives = _rush.Lives;
        int guard = 0;
        while (!_over && _rush.Lives == lives && guard++ < 4)
        {
            var wrong = FindLegalNonSolution();
            if (wrong == null) break;
            PlayMove(wrong);
            yield return new WaitForSeconds(0.9f);
        }
        yield return new WaitForSeconds(0.6f);
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

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
