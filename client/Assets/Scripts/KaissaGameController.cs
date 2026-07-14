using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using Kaissa.Training.Play;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Play a full game vs the adaptive bot (UI Toolkit + the 2D board). Drives KaissaGame (Stockfish
// from StreamingAssets) and owns the clock, rating update, post-game review and walkthrough.
public sealed class KaissaGameController : MonoBehaviour
{
    private KaissaGame _game;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;
    private bool _busy;
    private string _lastMove;
    private bool _whiteBottom = true;
    private bool _playerWhite = true;
    private bool _resignArmed;   // confirm-resign: first click arms, second resigns
    private bool _lowTimeWarned; // low-time sound plays once per game
    private string _currentFen = ChessGame.StartFen;
    private bool _canMove;

    private bool _reviewMode;
    private int _reviewPly;
    private string _gameStartFen;
    private IReadOnlyList<string> _reviewMoves;
    private IReadOnlyList<string> _reviewSan;
    private Dictionary<int, GameReviewItem> _reviewMistakes;
    private Dictionary<int, string> _reviewAll;      // 0-based player ply -> move quality (all moves)
    private IReadOnlyList<int> _reviewEval;           // player-POV cp after each player move (eval graph)
    private VisualElement _graphHost;

    private VisualElement _root;
    private VisualElement _movesBody;
    private Label _titleLabel;
    private Label _statusLabel;
    private Label _matLabel;
    private Label _topName;
    private Label _botName;
    private VisualElement _botCaptured;
    private VisualElement _youCaptured;
    private Label _botMat, _youMat;       // material advantage "+N" on the side that is ahead
    private Label _botClock;
    private Label _youClock;
    private double _playerRating;
    private bool _timed, _flagged, _activeWhite = true;
    private double _clockWhite, _clockBlack, _increment;
    private int _tcIndex;
    private int _pickSide; // 0 White, 1 Black, 2 Random
    private string _lastLabel; private int? _lastElo;
    private static readonly (string name, int secs, int inc)[] TimeControls =
    {
        ("Untimed", 0, 0), ("3 min", 180, 0), ("5 min", 300, 0), ("10 min", 600, 0), ("15|10", 900, 10),
    };
    private VisualElement _evalFill;
    private KaissaAnalysis _analysis;
    private CancellationTokenSource _evalCts;

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _audio = PieceAudio.Attach(gameObject);

        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        StartCoroutine(BuildUi(doc));
    }

    private System.Collections.IEnumerator BuildUi(UIDocument doc)
    {
        yield return null;
        _root = doc.rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Row;
        _root.style.flexGrow = 1;
        _root.style.backgroundColor = UiKit.Bg;

        _root.Add(UiKit.NavRail("Play"));
        _root.Add(BuildCenter());
        _root.Add(BuildRightRail());

        _board = BoardMount.Create(gameObject, _boardHost, _root, uci => OnMove(uci), _audio);
        _board.AllowPremove = true; // queue a move while the bot thinks
        if (KaissaSettings.EvalBar) StartCoroutine(StartAnalysis());

        if (Environment.GetCommandLineArgs().Contains("-kaissa-playtest"))
        {
            _tcIndex = 1; // 3-minute clock, to verify the timer ticks
            StartGame("Rookie", 1350);
            StartCoroutine(AutoPlay());
        }
        else if (EndgameRoute.Fen != null)
            StartGame("Bot", null);
        else if (Environment.GetCommandLineArgs().Contains("-annotate3d"))
            StartGame("Bot", null); // harness: plain game, no picker/autoplay, so annotations can be verified
        else if (RematchRoute.Active)
        {
            RematchRoute.Active = false;
            _tcIndex = RematchRoute.Tc;
            StartGame(RematchRoute.Label, RematchRoute.Elo >= 0 ? RematchRoute.Elo : (int?)null);
        }
        else
            ShowPicker();
    }

    private VisualElement BuildCenter()
    {
        var center = new VisualElement();
        center.style.flexGrow = 1;
        center.style.alignItems = Align.Center;
        UiKit.Pad(center, 24, 24, 24, 24);

        _titleLabel = UiKit.Text_("Play vs Bot", 24, UiKit.Text, bold: true);
        _titleLabel.style.marginBottom = 12;
        center.Add(_titleLabel);

        _botName = UiKit.Text_("Bot", 15, UiKit.Dim, bold: true);
        _botCaptured = CapturedRow();
        _botMat = UiKit.Text_("", 13, UiKit.Dim, bold: true);
        _botClock = ClockLabel();
        center.Add(Strip(_botName, _botCaptured, _botMat, _botClock));

        var boardRow = new VisualElement();
        boardRow.style.flexDirection = FlexDirection.Row;
        boardRow.style.alignItems = Align.Center;
        if (KaissaSettings.EvalBar)
        {
            var bar = new VisualElement();
            bar.style.width = 16; bar.style.height = 480; bar.style.marginRight = 8; bar.style.flexShrink = 0;
            bar.style.flexDirection = FlexDirection.ColumnReverse; bar.style.overflow = Overflow.Hidden;
            bar.style.backgroundColor = UiKit.Hex(0x40, 0x3d, 0x39); UiKit.Radius(bar, 4);
            _evalFill = new VisualElement();
            _evalFill.style.width = Length.Percent(100); _evalFill.style.height = Length.Percent(50);
            _evalFill.style.backgroundColor = UiKit.Hex(0xf4, 0xf4, 0xf4);
            bar.Add(_evalFill);
            boardRow.Add(bar);
        }
        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        boardRow.Add(_boardHost);
        center.Add(boardRow);

        _topName = UiKit.Text_("you", 15, UiKit.Text, bold: true);
        _youCaptured = CapturedRow();
        _youMat = UiKit.Text_("", 13, UiKit.Dim, bold: true);
        _youClock = ClockLabel();
        center.Add(Strip(_topName, _youCaptured, _youMat, _youClock));

        _statusLabel = UiKit.Text_("", 15, UiKit.Dim);
        _statusLabel.style.marginTop = 12;
        _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        center.Add(_statusLabel);
        return center;
    }

    private VisualElement Strip(Label name, VisualElement captured, Label mat, Label clock)
    {
        // Drawn avatar chip (no glyph): a small neutral disc next to the player name.
        var av = new VisualElement();
        av.style.width = 22; av.style.height = 22; UiKit.Radius(av, 11);
        av.style.backgroundColor = UiKit.Panel3; av.style.flexShrink = 0;
        av.style.marginRight = 8;
        captured.style.marginLeft = 10;
        mat.style.marginLeft = 6;
        var spacer = new VisualElement(); spacer.style.flexGrow = 1;
        var s = UiKit.Row(av, name, captured, mat, spacer, clock);
        s.style.width = 480; UiKit.Pad(s, 6, 4, 6, 4);
        return s;
    }

    private static VisualElement CapturedRow()
    {
        var r = UiKit.Row();
        r.style.flexShrink = 1; r.style.flexWrap = Wrap.NoWrap;
        return r;
    }

    // Fill a plate's tray with mini art of the captured pieces of the given colour (start minus current).
    private static void FillCaptured(VisualElement row, BoardView board, bool white)
    {
        row.Clear();
        var start = new Dictionary<char, int> { ['P'] = 8, ['N'] = 2, ['B'] = 2, ['R'] = 2, ['Q'] = 1 };
        var cur = new Dictionary<char, int> { ['P'] = 0, ['N'] = 0, ['B'] = 0, ['R'] = 0, ['Q'] = 0 };
        foreach (var p in board.Pieces)
            if (char.IsUpper(p.Piece) == white)
            {
                char u = char.ToUpperInvariant(p.Piece);
                if (cur.ContainsKey(u)) cur[u]++;
            }
        foreach (var k in new[] { 'Q', 'R', 'B', 'N', 'P' })
        {
            char code = white ? k : char.ToLowerInvariant(k);
            var tex = PieceArt.Get(code);
            for (int i = 0; i < start[k] - cur[k]; i++)
            {
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.style.width = 16; img.style.height = 18; img.style.marginRight = -3; // overlap slightly, chess.com-style
                if (tex != null)
                {
                    img.style.backgroundImage = new StyleBackground(tex);
                    img.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                }
                row.Add(img);
            }
        }
    }

    private static Label ClockLabel()
    {
        var l = UiKit.Text_("", 18, UiKit.Text, bold: true);
        l.style.backgroundColor = UiKit.Panel3;
        UiKit.Pad(l, 4, 12, 4, 12); UiKit.Radius(l, 6);
        l.style.display = DisplayStyle.None; // shown only for timed games
        return l;
    }

    private VisualElement BuildRightRail()
    {
        var rail = new VisualElement();
        rail.style.width = 340;
        UiKit.Pad(rail, 24, 24, 24, 0);

        var panel = new VisualElement();
        panel.style.backgroundColor = UiKit.Panel;
        panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 1;
        panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = UiKit.Line;
        UiKit.Radius(panel, 12);

        var hd = UiKit.Row(UiKit.Text_("Moves", 15, UiKit.Text, bold: true));
        hd.style.justifyContent = Justify.SpaceBetween;
        _matLabel = UiKit.Text_("", 13, UiKit.Mute, bold: true);
        hd.Add(_matLabel);
        UiKit.Pad(hd, 14, 16, 14, 16);
        hd.style.borderBottomWidth = 1; hd.style.borderBottomColor = UiKit.Line;
        panel.Add(hd);

        var scroll = UiKit.Scroll();
        scroll.style.maxHeight = 300;
        _movesBody = scroll.contentContainer;
        panel.Add(scroll);

        var ctrls = UiKit.Row(
            Ctrl("Takeback", Takeback), Ctrl("Resign", Resign), Ctrl("Flip", Flip), Ctrl("New", NewGame));
        UiKit.Pad(ctrls, 12, 12, 12, 12);
        ctrls.style.borderTopWidth = 1; ctrls.style.borderTopColor = UiKit.Line;
        panel.Add(ctrls);

        rail.Add(panel);
        _graphHost = new VisualElement();
        _graphHost.style.marginTop = 12;
        rail.Add(_graphHost);
        return rail;
    }

    private VisualElement Ctrl(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 12);
        b.style.flexGrow = 1; b.style.marginLeft = 3; b.style.marginRight = 3;
        return b;
    }

    private void ShowPicker()
    {
        var dim = Overlay();
        var panel = OverlayPanel();
        panel.Add(UiKit.Text_("Choose your opponent", 24, UiKit.Text, bold: true));

        var tcRow = UiKit.Row(); tcRow.style.marginTop = 12; tcRow.style.marginBottom = 12;
        var tcBtns = new List<VisualElement>();
        for (int i = 0; i < TimeControls.Length; i++)
        {
            int idx = i;
            var b = UiKit.Ghost(TimeControls[i].name, () =>
            {
                _tcIndex = idx;
                for (int j = 0; j < tcBtns.Count; j++)
                    tcBtns[j].style.backgroundColor = j == idx ? UiKit.Green : UiKit.Panel2;
            }, 12);
            b.style.marginLeft = 3; b.style.marginRight = 3;
            if (i == _tcIndex) b.style.backgroundColor = UiKit.Green;
            tcBtns.Add(b); tcRow.Add(b);
        }
        panel.Add(tcRow);

        var colorRow = UiKit.Row(); colorRow.style.marginBottom = 12;
        var colorNames = new[] { "White", "Black", "Random" };
        var colorBtns = new List<VisualElement>();
        for (int i = 0; i < colorNames.Length; i++)
        {
            int idx = i;
            var b = UiKit.Ghost(colorNames[i], () =>
            {
                _pickSide = idx;
                for (int j = 0; j < colorBtns.Count; j++)
                    colorBtns[j].style.backgroundColor = j == idx ? UiKit.Green : UiKit.Panel2;
            }, 12);
            b.style.marginLeft = 3; b.style.marginRight = 3;
            if (i == _pickSide) b.style.backgroundColor = UiKit.Green;
            colorBtns.Add(b); colorRow.Add(b);
        }
        panel.Add(colorRow);

        panel.Add(PickBtn("Adaptive - matches your level", () => { _root.Remove(dim); StartGame("Adaptive", null); }));
        foreach (var bot in BotRoster.All)
        {
            var b = bot;
            panel.Add(PickBtn($"{b.Name}  ({b.Elo})", () => { _root.Remove(dim); StartGame(b.Name, b.Elo); }));
        }
        var back = UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu"));
        back.style.marginTop = 8; back.style.width = 360;
        panel.Add(back);

        dim.Add(panel);
        _root.Add(dim);
    }

    private VisualElement PickBtn(string label, Action onClick)
    {
        var b = UiKit.Primary(label, onClick, 15);
        b.name = "pickopp";
        b.style.width = 360; b.style.marginBottom = 8;
        return b;
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

    private VisualElement OverlayPanel()
    {
        var p = new VisualElement();
        p.style.backgroundColor = UiKit.Panel;
        UiKit.Pad(p, 28); UiKit.Radius(p, 14);
        p.style.alignItems = Align.Center;
        return p;
    }

    private static int BotThinkMs() => KaissaSettings.BotSpeed switch { 0 => 250, 2 => 1200, _ => 600 };

    private bool _starting; // guards against concurrent engine ops from rapid New/Rematch clicks

    private async void StartGame(string label, int? fixedElo)
    {
        if (_starting) return;
        _starting = true;
        try { await StartGameCore(label, fixedElo); }
        finally { _starting = false; }
    }

    private async System.Threading.Tasks.Task StartGameCore(string label, int? fixedElo)
    {
        if (!EngineHub.Available)
        {
            _statusLabel.text = "Stockfish not found. Run scripts/build-unity-plugins.ps1.";
            return;
        }

        _lastLabel = label; _lastElo = fixedElo;
        _titleLabel.text = $"Play vs {label}";
        _statusLabel.text = "Starting engine...";
        double playerRating = KaissaTrainer.CreateDefault(KaissaProgress.Load()).PlayerRating;
        _playerRating = playerRating;
        var startFen = EndgameRoute.Fen;
        EndgameRoute.Fen = null;
        _gameStartFen = startFen ?? ChessGame.StartFen;
        if (startFen == null) // remember the last real opponent (not endgame drills) for the Home rematch card
        {
            KaissaSettings.LastOpponent = label;
            KaissaSettings.LastOpponentElo = fixedElo ?? -1;
            KaissaSettings.LastTc = _tcIndex;
        }
        // Resolve the chosen colour (0 White, 1 Black, 2 Random). An endgame-drill FEN forces White.
        _playerWhite = startFen != null ? true : _pickSide switch { 1 => false, 2 => UnityEngine.Random.value < 0.5f, _ => true };
        _whiteBottom = _playerWhite;
        var playerSide = _playerWhite ? Side.White : Side.Black;
        try
        {
            // Use the shared, app-wide play engine (spawned once at launch); reuse it across games.
            var engine = await EngineHub.PlayEngineAsync();
            if (_game == null)
                _game = await KaissaGame.AttachAsync(engine, playerSide, playerRating,
                    fen: startFen, botThinkTime: TimeSpan.FromMilliseconds(BotThinkMs()), fixedOpponentElo: fixedElo);
            else
                await _game.ResetAsync(playerSide, playerRating,
                    fen: startFen, botThinkTime: TimeSpan.FromMilliseconds(BotThinkMs()), fixedOpponentElo: fixedElo);
        }
        catch (Exception e)
        {
            _statusLabel.text = "Engine failed to start (see Console).";
            Debug.LogError(e);
            return;
        }

        _botName.text = $"{label}  ~{_game.OpponentElo}";
        _topName.text = $"you  {playerRating:0}";
        _statusLabel.text = $"You are {(_playerWhite ? "White" : "Black")}. {(_playerWhite ? "Your move." : "Bot moves first.")}   -   N new - R resign - U takeback - F flip";
        _lastMove = null;

        var tc = TimeControls[Mathf.Clamp(_tcIndex, 0, TimeControls.Length - 1)];
        _timed = tc.secs > 0; _clockWhite = _clockBlack = tc.secs; _increment = tc.inc;
        _activeWhite = _playerWhite; _flagged = false; // after any bot opening it is the player's turn
        _resignArmed = false; _lowTimeWarned = false;
        _botClock.style.display = _youClock.style.display = _timed ? DisplayStyle.Flex : DisplayStyle.None;
        UpdateClockLabels();

        RenderBoard(_game.Board.Fen, canMove: true);
        UpdateMoveList();
    }

    // Playtest driver (screenshot harness): play a couple of legal moves so the clock switches sides,
    // the bot replies, and captured pieces / move list populate - all observable in the frame captures.
    private System.Collections.IEnumerator AutoPlay()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        string tag = KaissaSettings.BoardView == 1 ? "3d" : "2d";

        KaissaSettings.AutoQueen = true;
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_warmup.png"));
        yield return new WaitForSeconds(1.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_start.png"));
        yield return new WaitForSeconds(0.5f);

        // Opening move through the real board input path: film the glide while the bot thinks.
        if (_game != null && !_busy) _board.DebugClickMove("e2", "e4");
        yield return Burst(dir, $"play_{tag}_move", 12, 0.06f);
        yield return new WaitForSeconds(2.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_afterbot.png"));

        if (_game != null && !_busy && !_game.IsGameOver) _board.DebugClickMove("g1", "f3");
        yield return new WaitForSeconds(2.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_midgame.png"));

        UiAutomation.Click(UiAutomation.FindButton(_root, "Flip")); yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_flip.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Flip")); yield return new WaitForSeconds(0.3f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Takeback")); yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_takeback.png"));

        UiAutomation.Click(UiAutomation.FindButton(_root, "Resign"));
        yield return new WaitForSeconds(3.0f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_review.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(_movesBody.Q("movecell")); yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_movenav.png"));
        yield return new WaitForSeconds(0.3f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "New")); yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_picker.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "5 min")); yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_picker_tc.png"));
        UiAutomation.Click(_root.Q("pickopp")); // Adaptive (first opponent button)
        yield return new WaitForSeconds(2.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"play_{tag}_newgame.png"));
        yield return new WaitForSeconds(0.4f);
        Application.Quit();
    }

    private System.Collections.IEnumerator Burst(string dir, string prefix, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{prefix}_{i:000}.png"));
            yield return new WaitForSeconds(interval);
        }
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key) return args[i + 1];
        return null;
    }

    private async void OnMove(string uci)
    {
        if (_busy || _game == null)
            return;
        _resignArmed = false; // making a move cancels a pending resign confirmation
        _busy = true;

        if (_timed && _flagged) { _busy = false; return; }
        var interFen = ApplyMove(_game.Board.Fen, uci);
        if (interFen != null)
            RenderBoard(interFen, canMove: false);
        _audio.PlayMove();
        if (_timed) // your clock stops (increment), bot's runs
        {
            if (_playerWhite) _clockWhite += _increment; else _clockBlack += _increment;
            _activeWhite = !_playerWhite;
        }

        try
        {
            var outcome = await _game.PlayAsync(uci);
            if (!outcome.Accepted)
            {
                _statusLabel.text = "Illegal move - try again.";
                RenderBoard(_game.Board.Fen, canMove: true);
                _busy = false;
                return;
            }

            _lastMove = string.IsNullOrEmpty(outcome.BotMove) ? uci : outcome.BotMove;
            RenderBoard(outcome.Board.Fen, canMove: !outcome.IsGameOver);
            UpdateMoveList();

            if (outcome.IsGameOver)
            {
                PlayResultCue(outcome.Result);
                _statusLabel.text = $"Game over: {outcome.Result}. Rating {_game.PlayerRating:0}. Reviewing...";
                var review = await _game.ReviewAsync();
                _statusLabel.text = $"Game over: {outcome.Result}. {review.Accuracy:0.0}% accuracy, " +
                                    $"{review.Mistakes.Count} mistake(s); {review.Practice.Count} to practice.  N: new game";
                EnterReview(review, ResultForPlayer(outcome.Result));
            }
            else
            {
                if (_timed) // bot moved (increment), your clock runs
                {
                    if (_playerWhite) _clockBlack += _increment; else _clockWhite += _increment;
                    _activeWhite = _playerWhite;
                }
                _statusLabel.text = $"Bot played {outcome.BotMove}. Your move.";
            }
            UpdateClockLabels();
        }
        catch (Exception e)
        {
            _statusLabel.text = "Engine error (see Console).";
            Debug.LogError(e);
        }
        _busy = false;
    }

    private void Takeback()
    {
        if (_game == null || _busy || _reviewMode || _game.IsGameOver || !_game.TryUndo())
            return;
        _lastMove = null;
        RenderBoard(_game.Board.Fen, canMove: true);
        UpdateMoveList();
        _statusLabel.text = "Takeback - your move.";
    }

    private async void Resign()
    {
        if (_game == null || _busy || _reviewMode || _game.IsGameOver)
            return;
        if (KaissaSettings.ConfirmResign && !_resignArmed)
        {
            _resignArmed = true;
            _statusLabel.text = "Click Resign again to confirm.";
            return;
        }
        _resignArmed = false;
        _busy = true;
        _audio.PlayDefeat();
        _statusLabel.text = "You resigned. Reviewing...";
        try
        {
            var review = await _game.ReviewAsync();
            _statusLabel.text = $"You resigned. {review.Accuracy:0.0}% accuracy, {review.Mistakes.Count} mistake(s); " +
                                $"{review.Practice.Count} to practice.  N: new game";
            EnterReview(review, 0); // resigning is a loss
        }
        catch (Exception e)
        {
            _statusLabel.text = "You resigned.  N: new game";
            Debug.LogError(e);
        }
    }

    private void NewGame()
    {
        // Keep the engine process alive so StartGame can reuse it (instant start, no respawn).
        _reviewMode = false;
        _busy = false;
        _lastMove = null;
        ShowPicker();
    }

    // Rematch: play the same opponent + time control again without the picker.
    private void Rematch()
    {
        if (_lastLabel == null) { NewGame(); return; }
        _reviewMode = false;
        _busy = false; _lastMove = null;
        StartGame(_lastLabel, _lastElo);
    }

    private void Flip()
    {
        _whiteBottom = !_whiteBottom;
        if (_reviewMode) RenderReviewPosition();
        else RenderBoard(_currentFen, _canMove);
    }

    private void RenderBoard(string fen, bool canMove)
    {
        _currentFen = fen;
        _canMove = canMove;
        _board.Render(fen, canMove, _lastMove, _whiteBottom);
        if (_analysis != null) EvaluateEval(fen);
    }

    private System.Collections.IEnumerator StartAnalysis()
    {
        if (!EngineHub.Available) yield break;
        var task = EngineHub.AnalysisEngineAsync();
        while (!task.IsCompleted) yield return null;
        if (task.IsFaulted) { Debug.LogError(task.Exception); yield break; }
        _analysis = KaissaAnalysis.Attach(task.Result);
        EvaluateEval(_currentFen);
    }

    private async void EvaluateEval(string fen)
    {
        if (_analysis == null || _evalFill == null) return;
        _evalCts?.Cancel();
        _evalCts = new CancellationTokenSource();
        var ct = _evalCts.Token;
        try
        {
            var line = await _analysis.EvaluateAsync(fen, depth: 12, ct);
            if (ct.IsCancellationRequested || _evalFill == null) return;
            bool whiteToMove = ChessGame.FromFen(fen).SideToMove == Side.White;
            int whiteCp = whiteToMove ? line.Centipawns : -line.Centipawns;
            _evalFill.style.height = Length.Percent((float)AccuracyModel.WinPercent(whiteCp));
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogError(e); }
    }

    private static string ApplyMove(string fen, string uci)
    {
        try
        {
            var game = ChessGame.FromFen(fen);
            if (game.TryMakeMove(uci))
                return game.Fen;
        }
        catch { }
        return null;
    }

    private void UpdateMoveList()
    {
        if (_movesBody == null || _game == null)
            return;
        _movesBody.Clear();

        int material = Material(_game.Board);
        _matLabel.text = material == 0 ? "even" : material > 0 ? $"White +{material}" : $"Black +{-material}";
        // Trays: next to each plate, the pieces that side captured (i.e. the other colour's losses).
        FillCaptured(_botCaptured, _game.Board, white: _playerWhite);   // bot captured the player's colour
        FillCaptured(_youCaptured, _game.Board, white: !_playerWhite);  // you captured the opponent's colour
        int forPlayer = _playerWhite ? material : -material;            // >0 = player ahead
        _youMat.text = forPlayer > 0 ? $"+{forPlayer}" : "";
        _botMat.text = forPlayer < 0 ? $"+{-forPlayer}" : "";

        var moves = _game.MoveHistorySan();
        for (int i = 0; i < moves.Count; i += 2)
        {
            string w = moves[i];
            string b = i + 1 < moves.Count ? moves[i + 1] : "";
            var wc = Cell(w, 120, UiKit.Text); int wply = i + 1;
            wc.name = "movecell";
            wc.RegisterCallback<ClickEvent>(_ => RowClicked(wply)); UiKit.Interactive(wc, 1.02f);
            MaybeBadge(wc, i);
            var bc = Cell(b, 120, UiKit.Text);
            if (!string.IsNullOrEmpty(b)) { int bply = i + 2; bc.name = "movecell"; bc.RegisterCallback<ClickEvent>(_ => RowClicked(bply)); UiKit.Interactive(bc, 1.02f); MaybeBadge(bc, i + 1); }
            var row = UiKit.Row(Cell($"{i / 2 + 1}.", 40, UiKit.Mute), wc, bc);
            if ((i / 2) % 2 == 1) row.style.backgroundColor = UiKit.Panel3;
            UiKit.Pad(row, 6, 12, 6, 12);
            _movesBody.Add(row);
        }
    }

    // A move-quality badge (coloured dot) on a player-move cell during review.
    private void MaybeBadge(VisualElement cell, int ply0)
    {
        if (!_reviewMode || _reviewAll == null || !_reviewAll.TryGetValue(ply0, out var quality)) return;
        var dot = new VisualElement { pickingMode = PickingMode.Ignore, tooltip = quality };
        dot.style.position = Position.Absolute; dot.style.right = 4; dot.style.top = 5;
        dot.style.width = 9; dot.style.height = 9; UiKit.Radius(dot, 5);
        dot.style.backgroundColor = QualityColor(quality);
        cell.Add(dot);
    }

    private static Color QualityColor(string quality) => quality switch
    {
        "Best" => UiKit.GreenHi,
        "Excellent" => UiKit.Green,
        "Good" => UiKit.GreenDeep,
        "Inaccuracy" => UiKit.Gold,
        "Mistake" => UiKit.Hex(0xe0, 0x82, 0x3a),
        "Blunder" => UiKit.Danger,
        _ => UiKit.Mute,
    };

    // Click a move in the list (during the post-game review) to jump to that position.
    private void RowClicked(int ply)
    {
        if (!_reviewMode || _reviewMoves == null) return;
        _reviewPly = Mathf.Clamp(ply, 0, _reviewMoves.Count);
        RenderReviewPosition();
    }

    private static Label Cell(string s, float w, Color c)
    {
        var l = UiKit.Text_(s, 14, c, bold: false);
        l.style.width = w;
        return l;
    }

    // Player-perspective result of a finished game: 2 win, 1 draw, 0 loss.
    private int ResultForPlayer(string result) => result switch
    {
        "WhiteWins" => _playerWhite ? 2 : 0,
        "BlackWins" => _playerWhite ? 0 : 2,
        "Draw" => 1,
        _ => 1,
    };

    private void EnterReview(GameReviewResult review, int result = 1)
    {
        if (review.Practice.Count > 0) KaissaPractice.Add(review.Practice);
        SaveRating();
        KaissaGameLog.Record(review.Accuracy, result);
        KaissaGameLog.RecordReview(
            System.Linq.Enumerable.Select(review.AllMoves, m => m.Quality),
            review.PhaseAccuracy.Opening, review.PhaseAccuracy.Middlegame, review.PhaseAccuracy.Endgame);

        _reviewMoves = _game.MoveHistory;
        _reviewSan = _game.MoveHistorySan();
        _reviewMistakes = new Dictionary<int, GameReviewItem>();
        foreach (var m in review.Mistakes)
            _reviewMistakes[(m.MoveNumber - 1) * 2 + (_playerWhite ? 0 : 1)] = m;
        // Every player move's quality, keyed by 0-based ply, for the move-list badges.
        _reviewAll = new Dictionary<int, string>();
        foreach (var a in review.AllMoves)
            _reviewAll[(a.MoveNumber - 1) * 2 + (_playerWhite ? 0 : 1)] = a.Quality;
        _reviewEval = review.EvalSeries;
        _reviewPly = _reviewMoves.Count;
        _reviewMode = true;
        _matLabel.text = $"acc {review.Accuracy:0}%";
        UpdateMoveList();
        BuildEvalGraph();
        RenderReviewPosition();
    }

    // A clickable advantage graph across the game (player POV): win-% area over the player's moves.
    private void BuildEvalGraph()
    {
        _graphHost.Clear();
        if (_reviewEval == null || _reviewEval.Count == 0) return;
        var panel = new VisualElement();
        panel.style.backgroundColor = UiKit.Panel;
        panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 1;
        panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = UiKit.Line;
        UiKit.Radius(panel, 12); UiKit.Pad(panel, 12, 14, 12, 14);
        panel.Add(UiKit.Text_("Advantage", 12, UiKit.Mute, bold: true));

        var graph = new VisualElement { name = "evalgraph" };
        graph.style.height = 90; graph.style.marginTop = 8;
        var eval = _reviewEval;
        graph.generateVisualContent += ctx =>
        {
            var r = graph.contentRect;
            if (r.width <= 1f || eval.Count == 0) return;
            var p = ctx.painter2D;
            float midY = r.height / 2f;
            // 50% (equal) baseline
            p.strokeColor = UiKit.Line; p.lineWidth = 1f;
            p.BeginPath(); p.MoveTo(new Vector2(0, midY)); p.LineTo(new Vector2(r.width, midY)); p.Stroke();
            // win-% curve (player POV): 0..100 mapped bottom..top
            p.strokeColor = UiKit.GreenHi; p.lineWidth = 2f; p.lineJoin = LineJoin.Round;
            p.BeginPath();
            for (int i = 0; i < eval.Count; i++)
            {
                float x = eval.Count == 1 ? r.width / 2f : r.width * i / (eval.Count - 1);
                float win = (float)AccuracyModel.WinPercent(eval[i]); // 0..100, player POV
                float y = r.height * (1f - win / 100f);
                if (i == 0) p.MoveTo(new Vector2(x, y)); else p.LineTo(new Vector2(x, y));
            }
            p.Stroke();
        };
        graph.RegisterCallback<ClickEvent>(e =>
        {
            var r = graph.contentRect;
            if (r.width <= 1f || eval.Count == 0) return;
            int idx = Mathf.Clamp(Mathf.RoundToInt(e.localPosition.x / r.width * (eval.Count - 1)), 0, eval.Count - 1);
            RowClicked((idx) * 2 + (_playerWhite ? 1 : 2)); // player move idx -> 1-based ply
        });
        panel.Add(graph);
        _graphHost.Add(panel);
    }

    private void RenderReviewPosition()
    {
        _lastMove = _reviewPly > 0 ? _reviewMoves[_reviewPly - 1] : null;
        RenderBoard(PositionAfter(_reviewPly), canMove: false);
        string move = _reviewPly > 0 && _reviewPly - 1 < _reviewSan.Count ? _reviewSan[_reviewPly - 1] : "start";
        string note = "";
        if (_reviewPly > 0 && _reviewMistakes.TryGetValue(_reviewPly - 1, out var m))
            note = $"   -   Mistake: best {m.BestMove} ({m.Quality})";
        _statusLabel.text = $"Review {_reviewPly}/{_reviewMoves.Count}: {move}   (<-/-> - N new){note}";
    }

    private string PositionAfter(int plyCount)
    {
        var game = ChessGame.FromFen(_gameStartFen);
        for (int i = 0; i < plyCount && i < _reviewMoves.Count; i++)
            game.TryMakeMove(_reviewMoves[i]);
        return game.Fen;
    }

    private void SaveRating()
    {
        if (_game == null) return;
        var saved = KaissaProgress.Load();
        var model = saved != null ? SkillModel.FromJson(saved) : new SkillModel();
        model.RatingEstimate = _game.PlayerRating;
        KaissaProgress.Save(model.ToJson());
    }

    private static int Material(BoardView board)
    {
        int sum = 0;
        foreach (var p in board.Pieces)
        {
            int v = char.ToUpperInvariant(p.Piece) switch { 'P' => 1, 'N' => 3, 'B' => 3, 'R' => 5, 'Q' => 9, _ => 0 };
            sum += char.IsUpper(p.Piece) ? v : -v;
        }
        return sum;
    }

    private void TickClock()
    {
        if (!_timed || _flagged || _game == null || _game.IsGameOver || _reviewMode) return;
        double dt = Time.deltaTime;
        if (_activeWhite) _clockWhite -= dt; else _clockBlack -= dt;
        // Warn once when the player's own clock (whichever colour) runs low on their turn.
        double playerClock = _playerWhite ? _clockWhite : _clockBlack;
        if (KaissaSettings.LowTimeWarning && !_lowTimeWarned && _activeWhite == _playerWhite && playerClock <= 10 && playerClock > 0)
        { _lowTimeWarned = true; _audio.PlayLowTime(); }
        // Flag attribution follows the chosen colour, not a fixed White-player assumption.
        if (_clockWhite <= 0) { _clockWhite = 0; _flagged = true; OnFlag(playerLost: _playerWhite); }
        else if (_clockBlack <= 0) { _clockBlack = 0; _flagged = true; OnFlag(playerLost: !_playerWhite); }
        UpdateClockLabels();
    }

    private void PlayResultCue(string result)
    {
        switch (result)
        {
            case "WhiteWins":
            case "BlackWins":
                bool playerWon = (result == "WhiteWins") == _playerWhite;
                if (playerWon) { _audio.PlayVictory(); BoardCelebrate.Burst(_boardHost); }
                else _audio.PlayDefeat();
                break;
            case "Draw": _audio.PlayDraw(); break;
            default: _audio.PlayGameEnd(); break;
        }
    }

    private void OnFlag(bool playerLost)
    {
        if (playerLost) _audio.PlayDefeat();
        else { _audio.PlayVictory(); BoardCelebrate.Burst(_boardHost); }
        _statusLabel.text = playerLost ? "Time - you lost.   -   N: new game" : "Time - the bot flagged. You win!   -   N: new game";
        RenderBoard(_currentFen, canMove: false);
    }

    private void UpdateClockLabels()
    {
        if (_botClock == null) return;
        if (!_timed) { _botClock.style.display = DisplayStyle.None; _youClock.style.display = DisplayStyle.None; return; }
        _youClock.text = Fmt(_playerWhite ? _clockWhite : _clockBlack);
        _botClock.text = Fmt(_playerWhite ? _clockBlack : _clockWhite);
        bool youActive = _activeWhite == _playerWhite;
        _youClock.style.color = youActive && !_flagged ? UiKit.Text : UiKit.Mute;
        _botClock.style.color = !youActive && !_flagged ? UiKit.Text : UiKit.Mute;
    }

    private static string Fmt(double seconds)
    {
        int t = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
        return $"{t / 60}:{t % 60:00}";
    }

    private void Update()
    {
        TickClock();
        if (Keyboard.current == null) return;

        if (_reviewMode)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame) SceneTransition.Go("Menu");
            else if (Keyboard.current.nKey.wasPressedThisFrame) NewGame();
            else if (Keyboard.current.rKey.wasPressedThisFrame) Rematch();
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame) { _reviewPly = Mathf.Max(0, _reviewPly - 1); RenderReviewPosition(); }
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) { _reviewPly = Mathf.Min(_reviewMoves.Count, _reviewPly + 1); RenderReviewPosition(); }
            else if (Keyboard.current.fKey.wasPressedThisFrame) Flip();
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame) SceneTransition.Go("Menu");
        else if (Keyboard.current.nKey.wasPressedThisFrame && _game != null) NewGame();
        else if (Keyboard.current.rKey.wasPressedThisFrame) Resign();
        else if (Keyboard.current.uKey.wasPressedThisFrame) Takeback();
        else if (Keyboard.current.fKey.wasPressedThisFrame) Flip();
    }

    private void OnDestroy()
    {
        if (_game != null) _ = _game.DisposeAsync();
        _evalCts?.Cancel();
        if (_analysis != null) _ = _analysis.DisposeAsync();
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
