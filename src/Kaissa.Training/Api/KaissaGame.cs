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
    private readonly GameSession _session;
    private readonly Side _playerSide;

    private KaissaGame(IChessEngine engine, GameSession session, Side playerSide)
    {
        _engine = engine;
        _session = session;
        _playerSide = playerSide;
    }

    /// <summary>Starts a game against an engine at the given executable path.</summary>
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
        await engine.NewGameAsync(cancellationToken).ConfigureAwait(false);

        var opponent = new AdaptiveOpponent(engine, botThinkTime ?? TimeSpan.FromMilliseconds(200), fixedElo: fixedOpponentElo);
        var session = new GameSession(engine, playerSide, playerRating, fen, opponent);
        var game = new KaissaGame(engine, session, playerSide);

        // If the player is Black, the bot (White) opens.
        if (session.SideToMove != playerSide && !session.IsGameOver)
            await session.EngineReplyAsync(cancellationToken).ConfigureAwait(false);

        return game;
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

    /// <summary>Reviews the game so far: the player's mistakes and the practice they generate.</summary>
    public async Task<GameReviewResult> ReviewAsync(CancellationToken cancellationToken = default)
    {
        var analyzer = new GameAnalyzer(_engine);
        var assessments = await analyzer.AnalyzeAsync(_session.StartFen, _session.MoveHistory, _playerSide, cancellationToken)
            .ConfigureAwait(false);

        var mistakes = assessments
            .Where(a => a.Quality > MoveQuality.Inaccuracy)
            .Select(a => new GameReviewItem(a.Ply / 2 + 1, a.PlayedMove, a.BestMove, a.Quality.ToString(), a.CentipawnLoss))
            .ToList();

        return new GameReviewResult(mistakes, GamePractice.FromAssessments(assessments));
    }

    public ValueTask DisposeAsync() => _engine.DisposeAsync();
}
