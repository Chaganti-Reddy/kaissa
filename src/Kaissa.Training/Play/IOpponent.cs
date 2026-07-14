namespace Kaissa.Training.Play;

/// <summary>
/// A computer opponent that picks a move for a position. Abstracts over the Stockfish-based adaptive
/// opponent and the Maia (lc0) human-like opponent, so a game does not care which is playing.
/// </summary>
public interface IOpponent
{
    /// <summary>Returns the opponent's move (UCI) for the given position.</summary>
    Task<string> ChooseMoveAsync(string fen, double playerRating, CancellationToken cancellationToken = default);

    /// <summary>The Elo this opponent represents (for the post-game rating update).</summary>
    int TargetElo(double playerRating);
}
