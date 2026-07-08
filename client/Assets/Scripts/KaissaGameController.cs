using System;
using System.Collections.Generic;
using System.IO;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
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
    private Board2D _board;
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

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _audio = PieceAudio.Attach(gameObject);
        _board = new Board2D(uci => OnMove(uci));

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
        var top = Strip(_botName);
        center.Add(top);

        var host = new VisualElement();
        host.style.width = 480; host.style.height = 480; host.style.flexShrink = 0;
        host.Add(_board.Root);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        center.Add(host);

        _topName = UiKit.Text_("you", 15, UiKit.Text, bold: true);
        center.Add(Strip(_topName));

        _statusLabel = UiKit.Text_("", 15, UiKit.Dim);
        _statusLabel.style.marginTop = 12;
        _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        center.Add(_statusLabel);
        return center;
    }

    private VisualElement Strip(Label name)
    {
        var av = UiKit.Text_("♟", 18, UiKit.Dim, bold: true);
        av.style.marginRight = 8;
        var s = UiKit.Row(av, name);
        s.style.width = 480; UiKit.Pad(s, 6, 4, 6, 4);
        return s;
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
        var spacer = new VisualElement(); spacer.style.height = 14; panel.Add(spacer);

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
        RenderBoard(_game.Board.Fen, canMove: true);
        UpdateMoveList();
    }

    private async void OnMove(string uci)
    {
        if (_busy || _game == null)
            return;
        _busy = true;

        var interFen = ApplyMove(_game.Board.Fen, uci);
        if (interFen != null)
            RenderBoard(interFen, canMove: false);
        _audio.PlayMove();

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
                _statusLabel.text = $"Bot played {outcome.BotMove}. Your move.";
            }
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

        var moves = _game.MoveHistorySan();
        for (int i = 0; i < moves.Count; i += 2)
        {
            string w = moves[i];
            string b = i + 1 < moves.Count ? moves[i + 1] : "";
            var row = UiKit.Row(
                Cell($"{i / 2 + 1}.", 40, UiKit.Mute),
                Cell(w, 120, UiKit.Text),
                Cell(b, 120, UiKit.Text));
            if ((i / 2) % 2 == 1) row.style.backgroundColor = UiKit.Panel3;
            UiKit.Pad(row, 6, 12, 6, 12);
            _movesBody.Add(row);
        }
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

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (_reviewMode)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame) SceneManager.LoadScene("Menu");
            else if (Keyboard.current.nKey.wasPressedThisFrame) NewGame();
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
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
