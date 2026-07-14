using Kaissa.Chess.Engine;
using Kaissa.Chess.Rules;
using Kaissa.Training.Play;

namespace Kaissa.Training.Api;

/// <summary>
/// The entry point a UI uses to play a full game against the adaptive bot and review it afterwards.
/// It owns the engine process, drives a <see cref="GameSession"/>, and speaks in plain DTOs. The
/// counterpart to <see cref="KaissaTrainer"/> for the play screen. Disposing it stops the engine.
/// </summary>
public sealed class KaissaGame : IAsyncDisposable
{
    private readonly IChessEngine _engine;
    private readonly bool _ownsEngine;
    private GameSession _session;
    private Side _playerSide;

    private KaissaGame(IChessEngine engine, GameSession session, Side playerSide, bool ownsEngine)
    {
        _engine = engine;
        _session = session;
        _playerSide = playerSide;
        _ownsEngine = ownsEngine;
    }

    /// <summary>Starts a game against an engine at the given executable path (owns that process).</summary>
    public static async Task<KaissaGame> StartAsync(
        string enginePath,
        Side playerSide,
        double playerRating,
        string? fen = null,
        TimeSpan? botThinkTime = null,
        int? fixedOpponentElo = null,
        CancellationToken cancellationToken = default)
    {
        var engine = UciChessEngine.LaunchProcess(enginePath);
        await engine.HandshakeAsync(cancellationToken).ConfigureAwait(false);
        return await AttachAsync(engine, playerSide, playerRating, fen, botThinkTime, fixedOpponentElo, ownsEngine: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Plays on an already-running engine (e.g. a shared, app-wide process). The engine is NOT owned:
    /// disposing this game leaves the process running for the next screen to reuse.
    /// </summary>
    public static async Task<KaissaGame> AttachAsync(
        IChessEngine engine,
        Side playerSide,
        double playerRating,
        string? fen = null,
        TimeSpan? botThinkTime = null,
        int? fixedOpponentElo = null,
        bool ownsEngine = false,
        CancellationToken cancellationToken = default)
    {
        await engine.NewGameAsync(cancellationToken).ConfigureAwait(false);

        var opponent = new AdaptiveOpponent(engine, botThinkTime ?? TimeSpan.FromMilliseconds(200), fixedElo: fixedOpponentElo);
        var session = new GameSession(engine, playerSide, playerRating, fen, opponent);
        var game = new KaissaGame(engine, session, playerSide, ownsEngine);

        // If the player is Black, the bot (White) opens.
        if (session.SideToMove != playerSide && !session.IsGameOver)
            await session.EngineReplyAsync(cancellationToken).ConfigureAwait(false);

        return game;
    }

    /// <summary>
    /// Reuses the already-running engine for a new game instead of launching a fresh process. This
    /// keeps switching drills / starting a new game near-instant (no ~1-2s process spawn + handshake);
    /// only a UCI "new game" reset is sent. Call this instead of disposing and StartAsync-ing again.
    /// </summary>
    public async Task ResetAsync(
        Side playerSide,
        double playerRating,
        string? fen = null,
        TimeSpan? botThinkTime = null,
        int? fixedOpponentElo = null,
        CancellationToken cancellationToken = default)
    {
        await _engine.NewGameAsync(cancellationToken).ConfigureAwait(false);
        var opponent = new AdaptiveOpponent(_engine, botThinkTime ?? TimeSpan.FromMilliseconds(200), fixedElo: fixedOpponentElo);
        _session = new GameSession(_engine, playerSide, playerRating, fen, opponent);
        _playerSide = playerSide;

        if (_session.SideToMove != playerSide && !_session.IsGameOver)
            await _session.EngineReplyAsync(cancellationToken).ConfigureAwait(false);
    }

    public BoardView Board => BoardView.FromFen(_session.Fen);

    /// <summary>Takes back the last full move (opponent reply + player move). False if nothing to undo.</summary>
    public bool TryUndo() => _session.TryUndoFullMove();

    /// <summary>The moves played so far (UCI), oldest first.</summary>
    public IReadOnlyList<string> MoveHistory => _session.MoveHistory;

    /// <summary>The moves played so far in SAN (e4, Nf3, O-O, ...), oldest first.</summary>
    public IReadOnlyList<string> MoveHistorySan() => _session.MoveHistorySan();

    public bool IsGameOver => _session.IsGameOver;
    public string Result => _session.Result.ToString();
    public int OpponentElo => _session.OpponentElo;
    public double PlayerRating => _session.PlayerRating;
    public IReadOnlyList<string> LegalMoves => _session.LegalUciMoves();

    /// <summary>
    /// Applies the player's move and, if the game continues, the bot's reply. If the move is
    /// illegal the position is unchanged and <see cref="MoveOutcome.Accepted"/> is false.
    /// </summary>
    public async Task<MoveOutcome> PlayAsync(string move, CancellationToken cancellationToken = default)
    {
        if (_session.IsGameOver || !_session.TryPlayerMove(move))
            return new MoveOutcome(false, null, null, Board, _session.IsGameOver, Result);

        string? botMove = null;
        if (!_session.IsGameOver)
            botMove = await _session.EngineReplyAsync(cancellationToken).ConfigureAwait(false);

        if (_session.IsGameOver)
            _session.FinalizeRating();

        return new MoveOutcome(true, move, botMove, Board, _session.IsGameOver, Result);
    }

    /// <summary>
    /// Reviews the whole game: per-move quality for both sides, each player's accuracy, the opening and
    /// how long it stayed in book, the turning points, a performance estimate, and practice from the
    /// player's mistakes.
    /// </summary>
    public async Task<GameReviewResult> ReviewAsync(CancellationToken cancellationToken = default)
    {
        var book = Kaissa.Training.OpeningBook.LoadDefault();
        var analyzer = new GameAnalyzer(_engine, book: book);
        var assessments = await analyzer.AnalyzeAsync(_session.StartFen, _session.MoveHistory, _playerSide, cancellationToken)
            .ConfigureAwait(false);

        var (openingName, openingEco, bookUntil) = DetectOpening(book, _session.StartFen, _session.MoveHistory);

        GameReviewItem ToItem(MoveAssessment a)
        {
            var motif = MoveClassifier.IsMistake(a.Quality)
                ? MotifClassifier.Classify(a.Fen, a.BestMove)
                : Motif.Unclassified;
            string commentary = MoveCommentary.Describe(a, motif, a.Quality == MoveQuality.Book ? openingName : null);
            return new GameReviewItem(a.Ply / 2 + 1, a.Side.ToString(), a.PlayedMove, a.PlayedMoveSan,
                a.BestMove, a.BestMoveSan, a.Quality.ToString(), a.CentipawnLoss, commentary);
        }

        var allMoves = assessments.Select(ToItem).ToList();
        var playerMoves = assessments.Where(a => a.Side == _playerSide).ToList();
        var opponentMoves = assessments.Where(a => a.Side != _playerSide).ToList();

        var mistakes = playerMoves.Where(a => MoveClassifier.IsMistake(a.Quality)).Select(ToItem).ToList();

        double accuracy = AccuracyModel.GameAccuracy(playerMoves);
        double opponentAccuracy = AccuracyModel.GameAccuracy(opponentMoves);

        // Player-perspective evaluation after every ply - the curve for the advantage graph.
        var evalSeries = assessments.Select(a => a.Side == _playerSide ? a.PlayedEvalCp : -a.PlayedEvalCp).ToList();
        var phaseAccuracy = AccuracyModel.ByPhase(playerMoves);

        // Turning points: brilliancies and the biggest errors, in game order.
        var keyMoments = assessments
            .Where(a => MoveClassifier.IsMistake(a.Quality) || a.Quality is MoveQuality.Brilliant or MoveQuality.Great)
            .OrderByDescending(a => a.Quality is MoveQuality.Brilliant or MoveQuality.Great ? 1_000_000 : a.CentipawnLoss)
            .Take(4)
            .OrderBy(a => a.Ply)
            .Select(ToItem)
            .ToList();

        int performance = PerformanceRating.Estimate(accuracy, _session.OpponentElo);
        var practice = GamePractice.FromAssessments(playerMoves);

        return new GameReviewResult(mistakes, practice, accuracy, opponentAccuracy, evalSeries, phaseAccuracy,
            allMoves, keyMoments, openingName, openingEco, bookUntil, performance);
    }

    // Walks the game against the opening book: the opening is the deepest named position reached while
    // every move so far was still a book continuation; bookUntil is that last full-move number.
    private static (string Name, string Eco, int BookUntil) DetectOpening(
        Kaissa.Training.OpeningBook book, string startFen, IReadOnlyList<string> moves)
    {
        var game = ChessGame.FromFen(startFen);
        string name = "", eco = "";
        int bookUntil = 0, ply = 0;
        foreach (var move in moves)
        {
            bool inBook = book.Continuations(game.Fen)
                .Any(c => string.Equals(c.Uci, move, StringComparison.OrdinalIgnoreCase));
            if (!game.TryMakeMove(move)) break;
            ply++;
            if (!inBook) break;
            bookUntil = (ply - 1) / 2 + 1;
            if (book.Name(game.Fen) is { } e) { name = e.Name; eco = e.Eco; }
        }
        return (name, eco, bookUntil);
    }

    public ValueTask DisposeAsync() => _ownsEngine ? _engine.DisposeAsync() : default;
}
