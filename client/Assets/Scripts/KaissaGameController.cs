using System;
using System.Collections.Generic;
using System.IO;
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

// Play a full game vs the adaptive bot, in the redesigned chess.com-style UI (UI Toolkit + the 2D
// board). Drives KaissaGame (Stockfish from StreamingAssets); keeps the rating update, review,
// walkthrough and practice-fusion logic — only the view changed from the old 3D/UGUI screen.
public sealed class KaissaGameController : MonoBehaviour
{
    private KaissaGame _game;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;
    private bool _busy;
    private string _lastMove;
    private bool _whiteBottom = true;
    private string _currentFen = ChessGame.StartFen;
    private bool _canMove;

    // review walkthrough
    private bool _reviewMode;
    private int _reviewPly;
    private string _gameStartFen;
    private IReadOnlyList<string> _reviewMoves;
    private IReadOnlyList<string> _reviewSan;
    private Dictionary<int, GameReviewItem> _reviewMistakes;

    private VisualElement _root;
    private VisualElement _movesBody;
    private Label _titleLabel;
    private Label _statusLabel;
    private Label _matLabel;
    private Label _topName;
    private Label _botName;
    private Label _botCaptured;
    private Label _youCaptured;
    private Label _botClock;
    private Label _youClock;
    private bool _timed, _flagged, _activeWhite = true;
    private double _clockWhite, _clockBlack, _increment;
    private int _tcIndex;
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

        if (EndgameRoute.Fen != null)
            StartGame("Bot", null);
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
        _botCaptured = UiKit.Text_("", 16, UiKit.Mute);
        _botClock = ClockLabel();
        center.Add(Strip(_botName, _botCaptured, _botClock));

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
        _youCaptured = UiKit.Text_("", 16, UiKit.Mute);
        _youClock = ClockLabel();
        center.Add(Strip(_topName, _youCaptured, _youClock));

        _statusLabel = UiKit.Text_("", 15, UiKit.Dim);
        _statusLabel.style.marginTop = 12;
        _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        center.Add(_statusLabel);
        return center;
    }

    private VisualElement Strip(Label name, Label captured, Label clock)
    {
        var av = UiKit.Text_("♟", 18, UiKit.Dim, bold: true);
        av.style.marginRight = 8;
        captured.style.marginLeft = 10;
        var spacer = new VisualElement(); spacer.style.flexGrow = 1;
        var s = UiKit.Row(av, name, captured, spacer, clock);
        s.style.width = 480; UiKit.Pad(s, 6, 4, 6, 4);
        return s;
    }

    private static Label ClockLabel()
    {
        var l = UiKit.Text_("", 18, UiKit.Text, bold: true);
        l.style.backgroundColor = UiKit.Panel3;
        UiKit.Pad(l, 4, 12, 4, 12); UiKit.Radius(l, 6);
        l.style.display = DisplayStyle.None; // shown only for timed games
        return l;
    }

    // Glyphs for the pieces of `white` colour that have been captured (start count minus current).
    private static string CapturedGlyphs(BoardView board, bool white)
    {
        var start = new Dictionary<char, int> { ['P'] = 8, ['N'] = 2, ['B'] = 2, ['R'] = 2, ['Q'] = 1 };
        var cur = new Dictionary<char, int> { ['P'] = 0, ['N'] = 0, ['B'] = 0, ['R'] = 0, ['Q'] = 0 };
        foreach (var p in board.Pieces)
            if (char.IsUpper(p.Piece) == white)
            {
                char u = char.ToUpperInvariant(p.Piece);
                if (cur.ContainsKey(u)) cur[u]++;
            }
        var glyph = new Dictionary<char, string> { ['P'] = "♟", ['N'] = "♞", ['B'] = "♝", ['R'] = "♜", ['Q'] = "♛" };
        var sb = new System.Text.StringBuilder();
        foreach (var k in new[] { 'Q', 'R', 'B', 'N', 'P' })
            for (int i = 0; i < start[k] - cur[k]; i++) sb.Append(glyph[k]);
        return sb.ToString();
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

        var scroll = new ScrollView();
        scroll.style.maxHeight = 300;
        _movesBody = scroll.contentContainer;
        panel.Add(scroll);

        var ctrls = UiKit.Row(
            Ctrl("Takeback", Takeback), Ctrl("Resign", Resign), Ctrl("Flip", Flip), Ctrl("New", NewGame));
        UiKit.Pad(ctrls, 12, 12, 12, 12);
        ctrls.style.borderTopWidth = 1; ctrls.style.borderTopColor = UiKit.Line;
        panel.Add(ctrls);

        rail.Add(panel);
        return rail;
    }

    private VisualElement Ctrl(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 12);
        b.style.flexGrow = 1; b.style.marginLeft = 3; b.style.marginRight = 3;
        return b;
    }

    // ---- opponent picker overlay ----
    private void ShowPicker()
    {
        var dim = Overlay();
        var panel = OverlayPanel();
        panel.Add(UiKit.Text_("Choose your opponent", 24, UiKit.Text, bold: true));

        // time control selector
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

        panel.Add(PickBtn("Adaptive — matches your level", () => { _root.Remove(dim); StartGame("Adaptive", null); }));
        foreach (var bot in BotRoster.All)
        {
            var b = bot;
            panel.Add(PickBtn($"{b.Name}  ({b.Elo})", () => { _root.Remove(dim); StartGame(b.Name, b.Elo); }));
        }
        var back = UiKit.Ghost("Back to menu", () => SceneManager.LoadScene("Menu"));
        back.style.marginTop = 8; back.style.width = 360;
        panel.Add(back);

        dim.Add(panel);
        _root.Add(dim);
    }

    private VisualElement PickBtn(string label, Action onClick)
    {
        var b = UiKit.Primary(label, onClick, 15);
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

    private async void StartGame(string label, int? fixedElo)
    {
        var enginePath = Path.Combine(Application.streamingAssetsPath, "stockfish", "stockfish.exe");
        if (!File.Exists(enginePath))
        {
            _statusLabel.text = "Stockfish not found. Run scripts/build-unity-plugins.ps1.";
            return;
        }

        _lastLabel = label; _lastElo = fixedElo;
        _titleLabel.text = $"Play vs {label}";
        _statusLabel.text = "Starting engine...";
        double playerRating = KaissaTrainer.CreateDefault(KaissaProgress.Load()).PlayerRating;
        var startFen = EndgameRoute.Fen;
        EndgameRoute.Fen = null;
        _gameStartFen = startFen ?? ChessGame.StartFen;
        try
        {
            _game = await KaissaGame.StartAsync(enginePath, Side.White, playerRating,
                fen: startFen, botThinkTime: TimeSpan.FromMilliseconds(BotThinkMs()), fixedOpponentElo: fixedElo);
        }
        catch (Exception e)
        {
            _statusLabel.text = "Engine failed to start (see Console).";
            Debug.LogError(e);
            return;
        }

        _botName.text = $"{label}  ~{_game.OpponentElo}";
        _statusLabel.text = "You are White. Your move.   ·   N new · R resign · U takeback · F flip";
        _lastMove = null;

        var tc = TimeControls[Mathf.Clamp(_tcIndex, 0, TimeControls.Length - 1)];
        _timed = tc.secs > 0; _clockWhite = _clockBlack = tc.secs; _increment = tc.inc;
        _activeWhite = true; _flagged = false;
        _botClock.style.display = _youClock.style.display = _timed ? DisplayStyle.Flex : DisplayStyle.None;
        UpdateClockLabels();

        RenderBoard(_game.Board.Fen, canMove: true);
        UpdateMoveList();
    }

    private async void OnMove(string uci)
    {
        if (_busy || _game == null)
            return;
        _busy = true;

        if (_timed && _flagged) { _busy = false; return; }
        var interFen = ApplyMove(_game.Board.Fen, uci);
        if (interFen != null)
            RenderBoard(interFen, canMove: false);
        _audio.PlayMove();
        if (_timed) { _clockWhite += _increment; _activeWhite = false; } // your clock stops, bot's runs

        try
        {
            var outcome = await _game.PlayAsync(uci);
            if (!outcome.Accepted)
            {
                _statusLabel.text = "Illegal move — try again.";
                RenderBoard(_game.Board.Fen, canMove: true);
                _busy = false;
                return;
            }

            _lastMove = string.IsNullOrEmpty(outcome.BotMove) ? uci : outcome.BotMove;
            RenderBoard(outcome.Board.Fen, canMove: !outcome.IsGameOver);
            UpdateMoveList();

            if (outcome.IsGameOver)
            {
                _audio.PlayGameEnd();
                _statusLabel.text = $"Game over: {outcome.Result}. Rating {_game.PlayerRating:0}. Reviewing...";
                var review = await _game.ReviewAsync();
                _statusLabel.text = $"Game over: {outcome.Result}. {review.Accuracy:0.0}% accuracy, " +
                                    $"{review.Mistakes.Count} mistake(s); {review.Practice.Count} to practice.  N: new game";
                EnterReview(review);
            }
            else
            {
                if (_timed) { _clockBlack += _increment; _activeWhite = true; } // bot moved, your clock runs
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
        _statusLabel.text = "Takeback — your move.";
    }

    private async void Resign()
    {
        if (_game == null || _busy || _reviewMode || _game.IsGameOver)
            return;
        _busy = true;
        _audio.PlayGameEnd();
        _statusLabel.text = "You resigned. Reviewing...";
        try
        {
            var review = await _game.ReviewAsync();
            _statusLabel.text = $"You resigned. {review.Accuracy:0.0}% accuracy, {review.Mistakes.Count} mistake(s); " +
                                $"{review.Practice.Count} to practice.  N: new game";
            EnterReview(review);
        }
        catch (Exception e)
        {
            _statusLabel.text = "You resigned.  N: new game";
            Debug.LogError(e);
        }
    }

    private void NewGame()
    {
        _reviewMode = false;
        if (_game != null) { _ = _game.DisposeAsync(); _game = null; }
        _busy = false;
        _lastMove = null;
        ShowPicker();
    }

    // Rematch: play the same opponent + time control again without the picker.
    private void Rematch()
    {
        if (_lastLabel == null) { NewGame(); return; }
        _reviewMode = false;
        if (_game != null) { _ = _game.DisposeAsync(); _game = null; }
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
        var enginePath = Path.Combine(Application.streamingAssetsPath, "stockfish", "stockfish.exe");
        if (!File.Exists(enginePath)) yield break;
        var task = KaissaAnalysis.StartAsync(enginePath);
        while (!task.IsCompleted) yield return null;
        if (task.IsFaulted) { Debug.LogError(task.Exception); yield break; }
        _analysis = task.Result;
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
        if (_botCaptured != null) _botCaptured.text = CapturedGlyphs(_game.Board, white: true);
        if (_youCaptured != null) _youCaptured.text = CapturedGlyphs(_game.Board, white: false);

        var moves = _game.MoveHistorySan();
        for (int i = 0; i < moves.Count; i += 2)
        {
            string w = moves[i];
            string b = i + 1 < moves.Count ? moves[i + 1] : "";
            var wc = Cell(w, 120, UiKit.Text); int wply = i + 1;
            wc.RegisterCallback<ClickEvent>(_ => RowClicked(wply));
            var bc = Cell(b, 120, UiKit.Text);
            if (!string.IsNullOrEmpty(b)) { int bply = i + 2; bc.RegisterCallback<ClickEvent>(_ => RowClicked(bply)); }
            var row = UiKit.Row(Cell($"{i / 2 + 1}.", 40, UiKit.Mute), wc, bc);
            if ((i / 2) % 2 == 1) row.style.backgroundColor = UiKit.Panel3;
            UiKit.Pad(row, 6, 12, 6, 12);
            _movesBody.Add(row);
        }
    }

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

    private void EnterReview(GameReviewResult review)
    {
        if (review.Practice.Count > 0) KaissaPractice.Add(review.Practice);
        SaveRating();
        KaissaGameLog.Record(review.Accuracy);

        _reviewMoves = _game.MoveHistory;
        _reviewSan = _game.MoveHistorySan();
        _reviewMistakes = new Dictionary<int, GameReviewItem>();
        foreach (var m in review.Mistakes)
            _reviewMistakes[(m.MoveNumber - 1) * 2] = m;
        _reviewPly = _reviewMoves.Count;
        _reviewMode = true;
        _matLabel.text = $"acc {review.Accuracy:0}%";
        RenderReviewPosition();
    }

    private void RenderReviewPosition()
    {
        _lastMove = _reviewPly > 0 ? _reviewMoves[_reviewPly - 1] : null;
        RenderBoard(PositionAfter(_reviewPly), canMove: false);
        string move = _reviewPly > 0 && _reviewPly - 1 < _reviewSan.Count ? _reviewSan[_reviewPly - 1] : "start";
        string note = "";
        if (_reviewPly > 0 && _reviewMistakes.TryGetValue(_reviewPly - 1, out var m))
            note = $"   —   Mistake: best {m.BestMove} ({m.Quality})";
        _statusLabel.text = $"Review {_reviewPly}/{_reviewMoves.Count}: {move}   (←/→ · N new){note}";
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
        if (_clockWhite <= 0) { _clockWhite = 0; _flagged = true; OnFlag(playerLost: true); }
        else if (_clockBlack <= 0) { _clockBlack = 0; _flagged = true; OnFlag(playerLost: false); }
        UpdateClockLabels();
    }

    private void OnFlag(bool playerLost)
    {
        _audio.PlayGameEnd();
        _statusLabel.text = playerLost ? "Time — you lost.   ·   N: new game" : "Time — the bot flagged. You win!   ·   N: new game";
        RenderBoard(_currentFen, canMove: false);
    }

    private void UpdateClockLabels()
    {
        if (_botClock == null) return;
        if (!_timed) { _botClock.style.display = DisplayStyle.None; _youClock.style.display = DisplayStyle.None; return; }
        _youClock.text = Fmt(_clockWhite);
        _botClock.text = Fmt(_clockBlack);
        _youClock.style.color = _activeWhite && !_flagged ? UiKit.Text : UiKit.Mute;
        _botClock.style.color = !_activeWhite && !_flagged ? UiKit.Text : UiKit.Mute;
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
            if (Keyboard.current.escapeKey.wasPressedThisFrame) SceneManager.LoadScene("Menu");
            else if (Keyboard.current.nKey.wasPressedThisFrame) NewGame();
            else if (Keyboard.current.rKey.wasPressedThisFrame) Rematch();
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame) { _reviewPly = Mathf.Max(0, _reviewPly - 1); RenderReviewPosition(); }
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) { _reviewPly = Mathf.Min(_reviewMoves.Count, _reviewPly + 1); RenderReviewPosition(); }
            else if (Keyboard.current.fKey.wasPressedThisFrame) Flip();
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame) SceneManager.LoadScene("Menu");
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
