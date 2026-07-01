namespace Kaissa.Training;

/// <summary>
/// Maintains a live estimate of the player's strength on the puzzle-rating (Elo) scale, updated
/// from each attempt as if the puzzle were an opponent: solving a hard puzzle raises the estimate,
/// failing an easy one lowers it. This feeds difficulty selection so practice stays at the edge
/// of the player's ability.
/// </summary>
public static class RatingEstimator
{
    /// <summary>Starting estimate for a new player (beginner-leaning).</summary>
    public const double Default = 800;

    private const double K = 32;
    private const double Min = 100;
    private const double Max = 3000;

    /// <summary>Probability the player solves a puzzle of the given rating.</summary>
    public static double ExpectedScore(double playerRating, int puzzleRating) =>
        1.0 / (1.0 + Math.Pow(10, (puzzleRating - playerRating) / 400.0));

    /// <summary>Returns the updated player rating after a puzzle attempt.</summary>
    public static double Update(double playerRating, int puzzleRating, bool solved) =>
        Update(playerRating, puzzleRating, solved ? 1.0 : 0.0);

    /// <summary>
    /// Returns the updated player rating after a result against an opponent of the given rating.
    /// <paramref name="score"/> is 1 for a win, 0.5 for a draw, 0 for a loss.
    /// </summary>
    public static double Update(double playerRating, int opponentRating, double score)
    {
        double expected = ExpectedScore(playerRating, opponentRating);
        double updated = playerRating + K * (score - expected);
        return Math.Clamp(updated, Min, Max);
    }
}
