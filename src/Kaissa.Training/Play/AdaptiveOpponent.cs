using Kaissa.Chess.Engine;

namespace Kaissa.Training.Play;

/// <summary>
/// A computer opponent that plays near the player's level. It caps the engine's strength to a
/// target Elo derived from the player's rating, so games stay competitive rather than crushing.
/// The engine sits behind <see cref="IChessEngine"/>, so a more human-like engine can be swapped
/// in later without changing this class.
/// </summary>
public sealed class AdaptiveOpponent
{
    // Stockfish's UCI_Elo limiter is defined over roughly this range.
    private const int MinElo = 1320;
    private const int MaxElo = 3190;

    private readonly IChessEngine _engine;
    private readonly TimeSpan _thinkTime;
    private readonly int _ratingOffset;

    public AdaptiveOpponent(IChessEngine engine, TimeSpan? thinkTime = null, int ratingOffset = 0)
    {
        _engine = engine;
        _thinkTime = thinkTime ?? TimeSpan.FromMilliseconds(100);
        _ratingOffset = ratingOffset;
    }

    /// <summary>The engine Elo this opponent will play at for a given player rating.</summary>
    public int TargetElo(double playerRating) =>
        (int)Math.Clamp(Math.Round(playerRating + _ratingOffset), MinElo, MaxElo);

    /// <summary>Returns the opponent's move (UCI) for the given position at the target strength.</summary>
    public async Task<string> ChooseMoveAsync(string fen, double playerRating, CancellationToken cancellationToken = default)
    {
        await _engine.ConfigureStrengthAsync(StrengthSettings.FromElo(TargetElo(playerRating)), cancellationToken)
            .ConfigureAwait(false);
        var result = await _engine.AnalyzeAsync(fen, SearchLimits.ForTime(_thinkTime), cancellationToken)
            .ConfigureAwait(false);
        return result.BestMove;
    }
}
