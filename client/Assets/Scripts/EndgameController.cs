using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Set by a screen before loading the Play scene to start a game from a specific position. Kept for
// compatibility; the Endgames page now plays drills on its own screen rather than routing to Play.
public static class EndgameRoute
{
    public static string Fen;
}

// Play an endgame out against Stockfish and get graded pass/fail against the drill goal (win/draw/
// promote), in free Practice or a timed Challenge run.
public sealed class EndgameController : MonoBehaviour
{
    private KaissaGame _game;
    private KaissaAnalysis _analysis;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;

    private EndgamePosition _current;
    private int _index;
    private bool _busy, _over, _whiteBottom = true;
    private string _fen = ChessGame.StartFen;

    // Challenge mode: a timed run through 5 drills back to back; fastest clean run is kept.
    private const int ChallengeLen = 5;
    private bool _challenge, _chRunning;
    private System.Collections.Generic.List<int> _chQueue = new();
    private int _chPos;
    private float _chStart;
    private readonly System.Random _rng = new();

    private VisualElement _root, _list;
    private VisualElement _sideBadge, _sideDot;
    private Label _sideText, _title, _goalLabel, _noteLabel, _feedback, _timerLabel;
    private Button _hintBtn, _retryBtn, _nextBtn, _practiceChip, _challengeChip;

    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.55f);
    private static readonly Color HintCol = new(0.51f, 0.72f, 0.30f, 0.60f);

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
        _root.Add(UiKit.NavRail("Endgame"));
        _root.Add(BuildCenter());
        _root.Add(BuildRightRail());

        _board = BoardMount.Create(gameObject, _boardHost, _root, uci => OnMove(uci), _audio);

        StartCoroutine(StartAnalysis());
        LoadDrill(0);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-endgametest"))
            StartCoroutine(AutoDemo());
    }

    private VisualElement BuildCenter()
    {
        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        _title = UiKit.Text_("Endgames", 24, UiKit.Text, bold: true);
        _title.style.marginBottom = 8;
        center.Add(_title);

        var modeRow = UiKit.Row(); modeRow.style.marginBottom = 8;
        _practiceChip = UiKit.Ghost("Practice", () => SetChallenge(false), 12);
        _challengeChip = UiKit.Ghost("Challenge", StartChallenge, 12);
        _practiceChip.style.marginRight = 6; _challengeChip.style.marginRight = 10;
        _timerLabel = UiKit.Text_("", 15, UiKit.Gold, bold: true);
        modeRow.Add(_practiceChip); modeRow.Add(_challengeChip); modeRow.Add(_timerLabel);
        center.Add(modeRow);
        RefreshModeChips();

        var sideRow = UiKit.Row();
        sideRow.style.width = 480; sideRow.style.justifyContent = Justify.SpaceBetween; sideRow.style.marginBottom = 8;
        _sideBadge = MakeSideBadge();
        sideRow.Add(_sideBadge);
        _goalLabel = UiKit.Text_("", 15, UiKit.Gold, bold: true);
        sideRow.Add(_goalLabel);
        center.Add(sideRow);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        center.Add(_boardHost);

        _feedback = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 10; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 6, 14, 6, 14); UiKit.Radius(_feedback, 8);
        center.Add(_feedback);

        var controls = UiKit.Row();
        controls.style.marginTop = 8;
        _hintBtn = Ctrl("Hint", ShowHint);
        _retryBtn = Ctrl("Retry", () => LoadDrill(_index));
        _nextBtn = Ctrl("Next", NextDrill);
        controls.Add(_hintBtn); controls.Add(_retryBtn); controls.Add(_nextBtn); controls.Add(Ctrl("Flip", Flip));
        center.Add(controls);
        return center;
    }

    private Button Ctrl(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 14);
        b.style.marginLeft = 5; b.style.marginRight = 5; b.style.minWidth = 84;
        return b;
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

    private VisualElement BuildRightRail()
    {
        var rail = new VisualElement();
        rail.style.width = 340; UiKit.Pad(rail, 18, 24, 18, 8);

        var info = Panel(); UiKit.Pad(info, 14, 16, 14, 16);
        info.Add(UiKit.Text_("Drill", 12, UiKit.Mute, bold: true));
        _noteLabel = UiKit.Text_("", 13, UiKit.Dim);
        _noteLabel.style.whiteSpace = WhiteSpace.Normal; _noteLabel.style.marginTop = 6;
        info.Add(_noteLabel);
        rail.Add(info);

        var listPanel = Panel(); listPanel.style.marginTop = 14; UiKit.Pad(listPanel, 12, 14, 12, 14);
        listPanel.Add(UiKit.Text_("All endgames", 12, UiKit.Mute, bold: true));
        var scroll = UiKit.Scroll(); scroll.style.maxHeight = 520; scroll.style.marginTop = 6;
        _list = scroll.contentContainer;
        listPanel.Add(scroll);
        rail.Add(listPanel);

        PopulateList();
        return rail;
    }

    private void PopulateList()
    {
        _list.Clear();
        foreach (var cat in EndgameLibrary.Categories)
        {
            var header = UiKit.Text_(cat, 12, UiKit.Mute, bold: true);
            header.style.marginTop = 8; header.style.marginBottom = 2;
            _list.Add(header);
            foreach (var e in EndgameLibrary.InCategory(cat))
            {
                int idx = IndexOf(e);
                var row = UiKit.Row(UiKit.Text_(e.Name, 14, UiKit.Text));
                row.name = "egrow-" + e.Id;
                UiKit.Pad(row, 7, 10, 7, 10); UiKit.Radius(row, 6);
                row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = UiKit.Panel2);
                row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = new Color(0, 0, 0, 0));
                row.RegisterCallback<ClickEvent>(_ => LoadDrill(idx));
                UiKit.Interactive(row, 1.02f);
                _list.Add(row);
            }
        }
    }

    private static int IndexOf(EndgamePosition e)
    {
        for (int i = 0; i < EndgameLibrary.All.Count; i++)
            if (EndgameLibrary.All[i].Id == e.Id) return i;
        return 0;
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

    private bool _loading; // guards against concurrent engine ops from rapid Next/Retry clicks

    private async void LoadDrill(int index)
    {
        if (EndgameLibrary.All.Count == 0 || _loading) return;
        _loading = true;
        try { await LoadDrillCore(index); }
        finally { _loading = false; }
    }

    private async System.Threading.Tasks.Task LoadDrillCore(int index)
    {
        _index = (index % EndgameLibrary.All.Count + EndgameLibrary.All.Count) % EndgameLibrary.All.Count;
        _current = EndgameLibrary.All[_index];
        _over = false; _busy = true;
        _title.text = _current.Name;
        _goalLabel.text = _current.GoalText;
        _noteLabel.text = _current.Note;
        // Only the first drill pays the one-time engine launch; later drills reuse the live process.
        SetFeedback(_game == null ? "Starting engine..." : _current.GoalText + ".", UiKit.Dim);
        _whiteBottom = _current.PlayerWhite;

        if (!EngineHub.Available)
        {
            SetFeedback("Stockfish not found. Run scripts/build-unity-plugins.ps1.", UiKit.Danger);
            _board.Render(_current.Fen, canMove: false, lastMove: null, whiteBottom: _whiteBottom);
            _busy = false; return;
        }

        var side = _current.PlayerWhite ? Side.White : Side.Black;
        try
        {
            // Shared, app-wide play engine (spawned once at launch); reused across every drill.
            var engine = await EngineHub.PlayEngineAsync();
            if (_game == null)
                _game = await KaissaGame.AttachAsync(engine, side, 1500,
                    fen: _current.Fen, botThinkTime: TimeSpan.FromMilliseconds(300), fixedOpponentElo: 2200);
            else
                await _game.ResetAsync(side, 1500,
                    fen: _current.Fen, botThinkTime: TimeSpan.FromMilliseconds(300), fixedOpponentElo: 2200);
        }
        catch (Exception e) { SetFeedback("Engine failed to start.", UiKit.Danger); Debug.LogError(e); _busy = false; return; }

        _fen = _game.Board.Fen;
        _board.Render(_fen, canMove: true, lastMove: null, whiteBottom: _whiteBottom);
        UpdateSideBadge();
        SetFeedback(_current.GoalText + ".", UiKit.Dim);
        _busy = false;
    }

    private void NextDrill() => LoadDrill(_index + 1);

    private async void OnMove(string uci)
    {
        if (_busy || _over || _game == null) return;
        _busy = true;

        var afterFen = ApplyMove(_fen, uci);
        if (afterFen != null) _board.Render(afterFen, canMove: false, lastMove: uci, whiteBottom: _whiteBottom);

        MoveOutcome outcome;
        try { outcome = await _game.PlayAsync(uci); }
        catch (Exception e) { Debug.LogError(e); _busy = false; return; }

        if (!outcome.Accepted)
        {
            _board.Render(_fen, canMove: true, lastMove: null, whiteBottom: _whiteBottom);
            SetFeedback("Illegal move.", UiKit.Danger);
            _busy = false; return;
        }

        _fen = outcome.Board.Fen;
        _board.Render(_fen, canMove: !outcome.IsGameOver, lastMove: outcome.BotMove ?? uci, whiteBottom: _whiteBottom);
        UpdateSideBadge();

        var result = DrillEvaluator.Evaluate(_fen, _current.PlayerWhite, _current.Goal);
        if (result == DrillOutcome.Passed) Conclude(true);
        else if (result == DrillOutcome.Failed) Conclude(false);
        else { SetFeedback(_current.GoalText + ".", UiKit.Dim); _audio.PlayMove(); }
        _busy = false;
    }

    private void Conclude(bool passed)
    {
        _over = true;
        if (passed) { _audio.PlayVictory(); BoardCelebrate.Burst(_boardHost); TintLastMove(Good); } else _audio.PlayWrong();
        Enable(_hintBtn, false);

        if (_challenge && _chRunning)
        {
            if (passed)
            {
                _chPos++;
                if (_chPos >= _chQueue.Count) { FinishChallenge(); return; }
                SetFeedback($"Solved - drill {_chPos + 1} of {_chQueue.Count}.", UiKit.GreenHi);
                StartCoroutine(NextChallengeDrill());
            }
            else
            {
                // A miss costs time: retry the same drill (the clock keeps running).
                SetFeedback("Not this time - the clock is running, try again.", UiKit.Danger);
                StartCoroutine(RetrySameDrill());
            }
            return;
        }

        SetFeedback(passed ? "Solved! Goal achieved." : "Not this time - Retry.", passed ? UiKit.GreenHi : UiKit.Danger);
    }

    private IEnumerator NextChallengeDrill()
    {
        yield return new WaitForSeconds(0.9f);
        if (_challenge) LoadDrill(_chQueue[_chPos]);
    }

    private IEnumerator RetrySameDrill()
    {
        yield return new WaitForSeconds(1.1f);
        if (_challenge) LoadDrill(_chQueue[_chPos]);
    }

    private void SetChallenge(bool on)
    {
        _challenge = on; _chRunning = false;
        _timerLabel.text = "";
        RefreshModeChips();
        if (!on) LoadDrill(_index);
    }

    private void StartChallenge()
    {
        var all = Enumerable.Range(0, EndgameLibrary.All.Count).ToList();
        for (int i = all.Count - 1; i > 0; i--) { int j = _rng.Next(i + 1); (all[i], all[j]) = (all[j], all[i]); }
        _chQueue = all.Take(Mathf.Min(ChallengeLen, all.Count)).ToList();
        _challenge = true; _chRunning = true; _chPos = 0; _chStart = Time.time;
        RefreshModeChips();
        SetFeedback($"Challenge: solve {_chQueue.Count} endgames as fast as you can.", UiKit.GreenHi);
        LoadDrill(_chQueue[0]);
    }

    private void FinishChallenge()
    {
        _chRunning = false; _challenge = false;
        int ms = Mathf.RoundToInt((Time.time - _chStart) * 1000f);
        bool best = KaissaSettings.EndgameChallengeBestMs == 0 || ms < KaissaSettings.EndgameChallengeBestMs;
        if (best) KaissaSettings.EndgameChallengeBestMs = ms;
        _timerLabel.text = FmtMs(ms);
        SetFeedback($"Challenge complete in {FmtMs(ms)}{(best ? " - new best!" : "")}", UiKit.GreenHi);
        RefreshModeChips();
    }

    private void RefreshModeChips()
    {
        if (_practiceChip == null) return;
        _practiceChip.style.backgroundColor = _challenge ? UiKit.Panel2 : UiKit.Green;
        _challengeChip.style.backgroundColor = _challenge ? UiKit.Green : UiKit.Panel2;
        if (!_chRunning && string.IsNullOrEmpty(_timerLabel.text))
        {
            int b = KaissaSettings.EndgameChallengeBestMs;
            _timerLabel.text = b > 0 ? $"Best {FmtMs(b)}" : "";
        }
    }

    private static string FmtMs(int ms) => $"{ms / 60000}:{(ms / 1000) % 60:00}.{(ms % 1000) / 100}";

    private IEnumerator StartAnalysis()
    {
        if (!EngineHub.Available) yield break;
        var task = EngineHub.AnalysisEngineAsync();
        while (!task.IsCompleted) yield return null;
        if (task.IsFaulted) { Debug.LogWarning(task.Exception?.Message); yield break; }
        _analysis = KaissaAnalysis.Attach(task.Result);
    }

    private async void ShowHint()
    {
        if (_analysis == null || _over || _game == null) return;
        try
        {
            var line = await _analysis.EvaluateAsync(_fen, depth: 14, CancellationToken.None);
            var best = line.BestMove;
            if (!string.IsNullOrEmpty(best) && best.Length >= 4)
            {
                _board.HighlightSquare(best.Substring(0, 2), HintCol);
                SetFeedback("Try the highlighted piece.", UiKit.Dim);
            }
        }
        catch (Exception e) { Debug.LogWarning(e.Message); }
    }

    private void Flip()
    {
        _whiteBottom = !_whiteBottom;
        _board.Render(_fen, canMove: !_over && !_busy, lastMove: null, whiteBottom: _whiteBottom);
    }

    private void Enable(Button b, bool on) { if (b != null) { b.SetEnabled(on); b.style.opacity = on ? 1f : 0.45f; } }

    private void UpdateSideBadge()
    {
        bool white = IsWhiteToMove(_fen);
        _sideText.text = white ? "White to move" : "Black to move";
        _sideDot.style.backgroundColor = white ? Color.white : new Color(0.10f, 0.10f, 0.12f);
        _sideBadge.style.backgroundColor = white ? new Color(1, 1, 1, 0.12f) : new Color(0, 0, 0, 0.28f);
    }

    private void SetFeedback(string text, Color color)
    {
        _feedback.text = text; _feedback.style.color = color;
        _feedback.style.backgroundColor = string.IsNullOrEmpty(text) ? new Color(0, 0, 0, 0) : new Color(0, 0, 0, 0.55f);
    }

    private void TintLastMove(Color c)
    {
        // Best-effort: highlight the player's last move squares if known via move history.
        var hist = _game?.MoveHistory;
        if (hist is { Count: > 0 })
        {
            var m = hist[hist.Count - 1];
            if (m.Length >= 4) { _board.HighlightSquare(m.Substring(0, 2), c); _board.HighlightSquare(m.Substring(2, 2), c); }
        }
    }

    private static bool IsWhiteToMove(string fen)
    {
        var p = fen.Split(' ');
        return p.Length < 2 || p[1] != "b";
    }

    private static string ApplyMove(string fen, string uci)
    {
        try { var g = ChessGame.FromFen(fen); if (g.TryMakeMove(uci)) return g.Fen; }
        catch { }
        return null;
    }

    private void Update()
    {
        if (_chRunning) _timerLabel.text = FmtMs(Mathf.RoundToInt((Time.time - _chStart) * 1000f));
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) SceneTransition.Go("Menu");
        else if (kb.fKey.wasPressedThisFrame) Flip();
        else if (kb.rKey.wasPressedThisFrame) LoadDrill(_index);
        else if (kb.nKey.wasPressedThisFrame) NextDrill();
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? Path.Combine(Application.persistentDataPath, "shots");
        Directory.CreateDirectory(dir);
        string tag = KaissaSettings.BoardView == 1 ? "3d" : "2d";

        KaissaSettings.AutoQueen = true;
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"eg_{tag}_warmup.png"));
        yield return new WaitForSeconds(2.5f); // let the first drill + engine start
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"eg_{tag}_start.png"));
        yield return new WaitForSeconds(0.5f);

        // Promotion drill: deterministic, one move to pass.
        UiAutomation.Click(_root.Q("egrow-kp_promote"));
        yield return new WaitForSeconds(2.5f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"eg_{tag}_drill.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(_hintBtn);
        yield return new WaitForSeconds(1.2f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"eg_{tag}_hint.png"));
        yield return new WaitForSeconds(0.4f);

        _board.DebugClickMove("e7", "e8");
        yield return new WaitForSeconds(1.6f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"eg_{tag}_passed.png"));
        yield return new WaitForSeconds(0.5f);

        UiAutomation.Click(_retryBtn);
        yield return new WaitForSeconds(2.2f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"eg_{tag}_retry.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(_nextBtn);
        yield return new WaitForSeconds(2.2f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Flip"));
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"eg_{tag}_next_flip.png"));
        yield return new WaitForSeconds(0.5f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Challenge"));
        yield return new WaitForSeconds(2.6f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"eg_{tag}_challenge.png"));
        yield return new WaitForSeconds(0.5f);
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
