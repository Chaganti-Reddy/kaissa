using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Play a full game against the adaptive bot (desktop). Drives KaissaGame, which runs Stockfish
// from StreamingAssets. Click a piece then a target to move; the bot replies; the rating updates
// and the game is reviewed at the end. Self-contained (builds its own scene/HUD) so it can run in
// its own scene. Placeholder visuals, like the training screen.
public sealed class KaissaGameController : MonoBehaviour
{
    private KaissaGame _game;
    private Transform _boardRoot;
    private bool _busy;
    private string _lastMove;

    private BoardInteractor _interactor;
    private PieceAudio _audio;
    private bool _whiteBottom = true;

    // Post-game review walkthrough (step through the finished game).
    private bool _reviewMode;
    private int _reviewPly;
    private string _gameStartFen;
    private IReadOnlyList<string> _reviewMoves;
    private IReadOnlyList<string> _reviewSan;
    private Dictionary<int, GameReviewItem> _reviewMistakes;

    private Text _titleText;
    private Text _statusText;
    private Text _moveListText;
    private Font _font;

    private Transform _pickerCanvas;

    private void Start()
    {
        _font = Hud.Font;
        SetUpCameraAndLight();
        BuildPostProcessing();
        BuildHud();
        _audio = PieceAudio.Attach(gameObject);
        _interactor = gameObject.AddComponent<BoardInteractor>();
        _interactor.Init(uci => OnMove(uci), _audio);
        _interactor.AllowPremove = true; // queue a move while the bot is thinking

        // An endgame position skips the picker and plays the adaptive opponent directly.
        if (EndgameRoute.Fen != null)
            StartGame("Bot", null, 200);
        else
            ShowOpponentPicker();
    }

    private void ShowOpponentPicker()
    {
        _pickerCanvas = Hud.Canvas();
        Hud.Text(_pickerCanvas, "Choose your opponent", 34, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 50f));

        float y = 200f;
        Hud.Button(_pickerCanvas, "Adaptive — matches your level", new Vector2(0f, y),
            () => StartGame("Adaptive", null, 250), 480f);
        y -= 66f;
        foreach (var bot in BotRoster.All)
        {
            var b = bot;
            Hud.Button(_pickerCanvas, $"{b.Name}  ({b.Elo})", new Vector2(0f, y),
                () => StartGame(b.Name, b.Elo, ThinkMsFor(b.Elo)), 480f);
            y -= 66f;
        }
        y -= 10f;
        Hud.Button(_pickerCanvas, "Back", new Vector2(0f, y),
            () => UnityEngine.SceneManagement.SceneManager.LoadScene("Menu"), 480f);
    }

    private static int ThinkMsFor(int elo) => elo switch
    {
        <= 1400 => 150,
        <= 1600 => 250,
        <= 1900 => 400,
        <= 2200 => 650,
        _ => 1000,
    };

    private async void StartGame(string label, int? fixedElo, int thinkMs)
    {
        if (_pickerCanvas != null) { Destroy(_pickerCanvas.gameObject); _pickerCanvas = null; }

        var enginePath = Path.Combine(Application.streamingAssetsPath, "stockfish", "stockfish.exe");
        if (!File.Exists(enginePath))
        {
            _statusText.text = "Stockfish not found. Run scripts/build-unity-plugins.ps1.";
            return;
        }

        _titleText.text = $"Play vs {label}";
        _statusText.text = "Starting engine...";
        double playerRating = KaissaTrainer.CreateDefault(KaissaProgress.Load()).PlayerRating;
        var startFen = EndgameRoute.Fen; // set by the endgame picker, if any
        EndgameRoute.Fen = null;
        _gameStartFen = startFen ?? ChessGame.StartFen; // for the review walkthrough replay
        try
        {
            _game = await KaissaGame.StartAsync(enginePath, Side.White, playerRating,
                fen: startFen, botThinkTime: TimeSpan.FromMilliseconds(thinkMs), fixedOpponentElo: fixedElo);
        }
        catch (Exception e)
        {
            _statusText.text = "Engine failed to start (see Console).";
            Debug.LogError(e);
            return;
        }

        _statusText.text = $"You are White. Bot ~{_game.OpponentElo} Elo. Your move.   ·   N new · R resign · U takeback · F flip";
        RenderBoard(_game.Board);
        _interactor.OnBoardRendered(_boardRoot, _game.Board, _lastMove, humanCanMove: true);
        UpdateMoveList();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (_reviewMode)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            else if (Keyboard.current.nKey.wasPressedThisFrame)
                NewGame();
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            { _reviewPly = Mathf.Max(0, _reviewPly - 1); RenderReviewPosition(); }
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            { _reviewPly = Mathf.Min(_reviewMoves.Count, _reviewPly + 1); RenderReviewPosition(); }
            else if (Keyboard.current.fKey.wasPressedThisFrame && _boardRoot != null)
            { _whiteBottom = !_whiteBottom; Board3D.OrientCamera(_whiteBottom); }
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
        else if (Keyboard.current.nKey.wasPressedThisFrame && _game != null && _pickerCanvas == null)
            NewGame();
        else if (Keyboard.current.rKey.wasPressedThisFrame && _game != null && !_busy
                 && !_game.IsGameOver && _pickerCanvas == null)
            Resign();
        else if (Keyboard.current.uKey.wasPressedThisFrame && _game != null && !_busy
                 && !_game.IsGameOver && _pickerCanvas == null)
            Takeback();
        else if (Keyboard.current.fKey.wasPressedThisFrame && _boardRoot != null)
        {
            _whiteBottom = !_whiteBottom;
            Board3D.OrientCamera(_whiteBottom);
        }
    }

    // Take back the last full move (yours + the bot's reply) and continue from there.
    private void Takeback()
    {
        if (!_game.TryUndo())
            return;
        _lastMove = null;
        RenderBoard(_game.Board);
        _interactor.OnBoardRendered(_boardRoot, _game.Board, _lastMove, humanCanMove: true);
        UpdateMoveList();
        _statusText.text = "Takeback — your move.   ·   N: new game   ·   R: resign   ·   U: takeback";
    }

    // Resign the current game: stop accepting moves and still review what was played.
    private async void Resign()
    {
        _busy = true;
        _interactor.SetInputEnabled(false);
        _audio.PlayGameEnd();
        _statusText.text = "You resigned. Reviewing...";
        try
        {
            var review = await _game.ReviewAsync();
            _statusText.text = $"You resigned. {review.Mistakes.Count} mistake(s); " +
                               $"{review.Practice.Count} added to practice.   ·   N: new game";
            EnterReview(review);
        }
        catch (Exception e)
        {
            _statusText.text = "You resigned.   ·   N: new game";
            Debug.LogError(e);
        }
    }

    // Restart from the opponent picker without returning to the main menu.
    private void NewGame()
    {
        _reviewMode = false;
        if (_game != null) { _ = _game.DisposeAsync(); _game = null; }
        _busy = false;
        _lastMove = null;
        _interactor.SetInputEnabled(false);
        if (_boardRoot != null) { Destroy(_boardRoot.gameObject); _boardRoot = null; }
        ShowOpponentPicker();
    }

    // Invoked by the BoardInteractor with a fully-formed UCI move (including any promotion letter).
    private async void OnMove(string uci)
    {
        if (_busy || _game == null)
            return;
        _busy = true;
        _interactor.SetInputEnabled(false);

        // Show the player's move at once and let a premove queue on the correct position while the
        // bot thinks; the final board (after the bot replies) is rendered when PlayAsync returns.
        var interFen = ApplyMove(_game.Board.Fen, uci);
        if (interFen != null)
        {
            var interBoard = BoardView.FromFen(interFen);
            RenderBoard(interBoard);
            _interactor.OnBoardRendered(_boardRoot, interBoard, uci, humanCanMove: false);
        }

        try
        {
            var outcome = await _game.PlayAsync(uci);
            if (!outcome.Accepted)
            {
                _statusText.text = "Illegal move — try again.";
                RenderBoard(_game.Board);
                _interactor.OnBoardRendered(_boardRoot, _game.Board, _lastMove, humanCanMove: true);
                _busy = false;
                return;
            }

            _lastMove = string.IsNullOrEmpty(outcome.BotMove) ? uci : outcome.BotMove!;
            RenderBoard(outcome.Board);
            UpdateMoveList();
            if (!string.IsNullOrEmpty(outcome.BotMove))
                _audio.PlayMove(); // the bot's reply; check is cued by OnBoardRendered

            if (outcome.IsGameOver)
            {
                _audio.PlayGameEnd();
                _interactor.OnBoardRendered(_boardRoot, outcome.Board, _lastMove, humanCanMove: false);
                _statusText.text = $"Game over: {outcome.Result}. Rating {_game.PlayerRating:0}. Reviewing...";
                var review = await _game.ReviewAsync();
                _statusText.text = $"Game over: {outcome.Result}. Rating {_game.PlayerRating:0}. " +
                                   $"{review.Mistakes.Count} mistake(s); {review.Practice.Count} added to practice.  " +
                                   "Press N for a new game.";
                EnterReview(review);
            }
            else
            {
                _statusText.text = $"Bot played {outcome.BotMove}. Your move.";
                _interactor.OnBoardRendered(_boardRoot, outcome.Board, _lastMove, humanCanMove: true);
            }
        }
        catch (Exception e)
        {
            _statusText.text = "Engine error (see Console).";
            Debug.LogError(e);
        }

        _busy = false;
    }

    private static string ApplyMove(string fen, string uci)
    {
        try
        {
            var game = ChessGame.FromFen(fen);
            if (game.TryMakeMove(uci))
                return game.Fen;
        }
        catch { /* fall through */ }
        return null;
    }

    private void RenderBoard(BoardView board)
    {
        if (_boardRoot != null)
            Destroy(_boardRoot.gameObject);
        _boardRoot = Board3D.Render(board); // shared base/tiles/theme/pieces/coordinates
    }

    private void BuildHud()
    {
        var canvasObj = new GameObject("HUD");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        _titleText = MakeText(canvas.transform, 26, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(1100f, 50f));
        _statusText = MakeText(canvas.transform, 22, TextAnchor.LowerCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 60f));
        _moveListText = MakeText(canvas.transform, 18, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -84f), new Vector2(240f, 760f));
    }

    private void UpdateMoveList()
    {
        if (_moveListText == null || _game == null)
            return;
        var moves = _game.MoveHistorySan();
        var sb = new StringBuilder();

        int material = Material(_game.Board);
        string mat = material == 0 ? "Material: even"
            : material > 0 ? $"Material: White +{material}"
            : $"Material: Black +{-material}";
        sb.AppendLine(mat);
        sb.AppendLine();

        for (int i = 0; i < moves.Count; i += 2)
        {
            string w = moves[i];
            string b = i + 1 < moves.Count ? moves[i + 1] : "";
            sb.AppendLine($"{i / 2 + 1,2}. {w,-6}{b}");
        }
        _moveListText.text = sb.ToString();
    }

    // After a game, replace the move list with a review: each flagged mistake with the move played
    // (SAN), the engine's best, its severity, and the centipawn loss.
    private void ShowReview(GameReviewResult review)
    {
        if (_moveListText == null)
            return;
        var san = _game.MoveHistorySan();
        var sb = new StringBuilder();
        sb.AppendLine($"Review — {review.Mistakes.Count} mistake(s)");
        sb.AppendLine();
        foreach (var m in review.Mistakes)
        {
            int ply = (m.MoveNumber - 1) * 2; // player is White → even plies
            string played = ply >= 0 && ply < san.Count ? san[ply] : m.PlayedMove;
            sb.AppendLine($"{m.MoveNumber,2}. {played,-7}");
            sb.AppendLine($"    best {m.BestMove}");
            sb.AppendLine($"    {m.Quality} -{m.CentipawnLoss}");
        }
        _moveListText.text = sb.ToString();
    }

    // Enter the walkthrough: step through the finished game with ←/→, mistakes flagged.
    private void EnterReview(GameReviewResult review)
    {
        ShowReview(review);
        _reviewMoves = _game.MoveHistory;
        _reviewSan = _game.MoveHistorySan();
        _reviewMistakes = new Dictionary<int, GameReviewItem>();
        foreach (var m in review.Mistakes)
            _reviewMistakes[(m.MoveNumber - 1) * 2] = m; // player is White → even plies
        _reviewPly = _reviewMoves.Count;
        _reviewMode = true;
        _interactor.SetInputEnabled(false);
        RenderReviewPosition();
    }

    private void RenderReviewPosition()
    {
        RenderBoard(BoardView.FromFen(PositionAfter(_reviewPly)));
        if (_reviewPly > 0)
            BoardFx.LastMove(_boardRoot, _reviewMoves[_reviewPly - 1]);

        string move = _reviewPly > 0 && _reviewPly - 1 < _reviewSan.Count ? _reviewSan[_reviewPly - 1] : "start";
        string note = "";
        if (_reviewPly > 0 && _reviewMistakes.TryGetValue(_reviewPly - 1, out var m))
            note = $"   —   Mistake: best {m.BestMove} ({m.Quality})";
        _statusText.text = $"Review {_reviewPly}/{_reviewMoves.Count}: {move}   (←/→ step · N new game){note}";
    }

    private string PositionAfter(int plyCount)
    {
        var game = ChessGame.FromFen(_gameStartFen);
        for (int i = 0; i < plyCount && i < _reviewMoves.Count; i++)
            game.TryMakeMove(_reviewMoves[i]);
        return game.Fen;
    }

    // Net material from White's perspective (P1 N3 B3 R5 Q9).
    private static int Material(BoardView board)
    {
        int sum = 0;
        foreach (var p in board.Pieces)
        {
            int v = char.ToUpperInvariant(p.Piece) switch
            {
                'P' => 1, 'N' => 3, 'B' => 3, 'R' => 5, 'Q' => 9, _ => 0,
            };
            sum += char.IsUpper(p.Piece) ? v : -v;
        }
        return sum;
    }

    private Text MakeText(Transform parent, int size, TextAnchor anchor, Vector2 anchorMinMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject("text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var rt = text.rectTransform;
        rt.anchorMin = anchorMinMax;
        rt.anchorMax = anchorMinMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        return text;
    }

    private void OnDestroy()
    {
        if (_game != null)
            _ = _game.DisposeAsync();
    }


    private static void SetUpCameraAndLight()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(3.5f, 7.5f, -4.5f);
            cam.transform.LookAt(new Vector3(3.5f, 0f, 3.2f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
                data.renderPostProcessing = true;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.30f, 0.36f);

        if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
        {
            var keyObj = new GameObject("KeyLight");
            var key = keyObj.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.96f, 0.9f);
            key.shadows = LightShadows.Soft;
            keyObj.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        }

        SceneEnvironment.Apply();
    }

    private static void BuildPostProcessing()
    {
        var volumeObj = new GameObject("PostFX");
        var volume = volumeObj.AddComponent<Volume>();
        volume.isGlobal = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;

        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.Neutral;

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.30f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.40f;

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.3f;
    }
}
