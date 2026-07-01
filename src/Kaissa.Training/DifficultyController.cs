namespace Kaissa.Training;

/// <summary>
/// Chooses which scenario of a pattern to present, aiming for "desirable difficulty": a puzzle
/// rated a little above the player's current estimate, so success is likely but not guaranteed.
/// This is the flow-channel selector; a future ML model can replace it behind the same call.
/// </summary>
public sealed class DifficultyController
{
    private readonly double _targetOffset;

    /// <param name="targetOffset">How far above the player's rating to aim, in Elo points.</param>
    public DifficultyController(double targetOffset = 50) => _targetOffset = targetOffset;

    /// <summary>
    /// Picks the scenario whose rating is closest to the player's target level, avoiding an
    /// immediate repeat of <paramref name="avoidId"/> when an alternative exists.
    /// </summary>
    public Scenario Pick(IReadOnlyList<Scenario> scenarios, double playerRating, string? avoidId = null)
    {
        if (scenarios.Count == 0)
            throw new ArgumentException("No scenarios to choose from.", nameof(scenarios));

        double target = playerRating + _targetOffset;

        var candidates = scenarios.Count > 1 && avoidId is not null
            ? scenarios.Where(s => s.Id != avoidId)
            : scenarios;

        return candidates
            .OrderBy(s => Math.Abs(s.Rating - target))
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .First();
    }
}
