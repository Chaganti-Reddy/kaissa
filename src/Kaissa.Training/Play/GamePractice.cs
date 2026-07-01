namespace Kaissa.Training.Play;

/// <summary>
/// Turns the mistakes a player made in a game into practice scenarios: each becomes a position
/// with the move they missed as the solution. Fed back into the training loop, a player's own
/// blunders return as spaced practice — the point where playing and learning meet.
/// </summary>
public static class GamePractice
{
    /// <summary>The pattern under which game-derived practice positions are filed.</summary>
    public static Pattern Pattern { get; } = new(
        new PatternId("tactic.from_your_games"),
        "From your games",
        "A position where you went wrong — find the move you missed.");

    /// <summary>
    /// Builds practice scenarios from the assessments at or below the given quality
    /// (mistakes and blunders by default).
    /// </summary>
    public static IReadOnlyList<Scenario> FromAssessments(
        IEnumerable<MoveAssessment> assessments, int rating = 1200, MoveQuality worseThan = MoveQuality.Inaccuracy)
    {
        return assessments
            .Where(a => a.Quality > worseThan)
            .Select(a => new Scenario(
                $"yourgame-{a.Ply}",
                Pattern.Id,
                a.Fen,
                new[] { a.BestMove },
                "You went wrong here. Find the move you missed.",
                rating))
            .ToList();
    }
}
