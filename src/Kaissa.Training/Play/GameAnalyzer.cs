using Kaissa.Chess.Engine;
using Kaissa.Chess.Rules;

namespace Kaissa.Training.Play;

/// <summary>How one of the player's moves stacked up against the engine's best.</summary>
public sealed record MoveAssessment(
    int Ply,
    string Fen,
    string PlayedMove,
    string BestMove,
    int CentipawnLoss,
    MoveQuality Quality,
    int BestEvalCp);

/// <summary>
/// Reviews the player's moves in a finished game with the engine, at full strength, producing a
/// per-move assessment. Mistakes and blunders from this feed the practice generator, so playing a
/// game becomes training.
/// </summary>
public sealed class GameAnalyzer
{
    private readonly IChessEngine _engine;
    private readonly int _depth;

    public GameAnalyzer(IChessEngine engine, int depth = 14)
    {
        _engine = engine;
        _depth = depth;
    }

    public async Task<IReadOnlyList<MoveAssessment>> AnalyzeAsync(
        string startFen, IReadOnlyList<string> moves, Side playerSide, CancellationToken cancellationToken = default)
    {
        await _engine.ConfigureStrengthAsync(StrengthSettings.Full, cancellationToken).ConfigureAwait(false);

        var assessments = new List<MoveAssessment>();
        var game = ChessGame.FromFen(startFen);
        int ply = 0;

        foreach (var move in moves)
        {
            if (game.SideToMove == playerSide)
                assessments.Add(await AssessAsync(game.Fen, move, ply, cancellationToken).ConfigureAwait(false));

            game.TryMakeMove(move);
            ply++;
        }

        return assessments;
    }

    private async Task<MoveAssessment> AssessAsync(string fen, string playedMove, int ply, CancellationToken ct)
    {
        var before = await _engine.AnalyzeAsync(fen, SearchLimits.ToDepth(_depth), ct).ConfigureAwait(false);
        int bestCp = before.Evaluation is { } bestEval ? MoveClassifier.ToCentipawns(bestEval) : 0;

        var probe = ChessGame.FromFen(fen);
        probe.TryMakeMove(playedMove);

        int playedCp;
        if (probe.IsGameOver)
        {
            // The player's move ended the game; score it from the player's side.
            playedCp = probe.Result switch
            {
                GameResult.Draw => 0,
                GameResult.WhiteWins => game_SideWasWhite(fen) ? 100_000 : -100_000,
                GameResult.BlackWins => game_SideWasWhite(fen) ? -100_000 : 100_000,
                _ => 0,
            };
        }
        else
        {
            var after = await _engine.AnalyzeAsync(probe.Fen, SearchLimits.ToDepth(_depth), ct).ConfigureAwait(false);
            // After the move it is the opponent to move, so negate to the player's perspective.
            playedCp = after.Evaluation is { } afterEval ? -MoveClassifier.ToCentipawns(afterEval) : 0;
        }

        int loss = Math.Max(0, bestCp - playedCp);
        return new MoveAssessment(ply, fen, playedMove, before.BestMove, loss, MoveClassifier.Classify(loss), bestCp);
    }

    private static bool game_SideWasWhite(string fen) =>
        ChessGame.FromFen(fen).SideToMove == Side.White;
}
