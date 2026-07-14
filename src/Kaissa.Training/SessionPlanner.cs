namespace Kaissa.Training;

/// <summary>
/// Chooses which pattern to practise next. The policy keeps the player on their frontier:
/// review what is due first, then introduce something new, then reinforce the weakest pattern.
/// This is where a future adaptive/ML selector plugs in.
/// </summary>
public sealed class SessionPlanner
{
    /// <summary>Returns the next pattern to practise, or null if the library has no patterns.</summary>
    public PatternId? NextPattern(SkillModel model, ScenarioLibrary library, DateTime now)
    {
        var patterns = library.Patterns;
        if (patterns.Count == 0)
            return null;

        var mostOverdue = model.Due(now)
            .OrderBy(c => c.DueUtc)
            .Select(c => (PatternId?)c.Pattern)
            .FirstOrDefault();
        if (mostOverdue is not null)
            return mostOverdue;

        foreach (var pattern in patterns)
        {
            if (!model.Has(pattern))
                return pattern;
        }

        return model.Cards
            .OrderBy(c => c.State?.Stability ?? 0)
            .Select(c => c.Pattern)
            .First();
    }
}
