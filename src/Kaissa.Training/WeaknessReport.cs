namespace Kaissa.Training;

/// <summary>
/// Finds the patterns a player is weakest at and builds a focused practice set from them, using
/// the skill model to target what needs work.
/// </summary>
public static class WeaknessReport
{
    /// <summary>Seen patterns ordered weakest-first (least stable memory, then most lapses).</summary>
    public static IReadOnlyList<PatternId> WeakestPatterns(SkillModel model, int count)
    {
        return model.Cards
            .Where(c => c.Seen)
            .OrderBy(c => c.State!.Value.Stability)
            .ThenByDescending(c => c.Lapses)
            .Take(count)
            .Select(c => c.Pattern)
            .ToList();
    }

    /// <summary>A practice set drawn from the weakest patterns.</summary>
    public static IReadOnlyList<Scenario> BuildPracticeSet(
        SkillModel model, ScenarioLibrary library, int patternCount = 3, int perPattern = 3)
    {
        var set = new List<Scenario>();
        foreach (var pattern in WeakestPatterns(model, patternCount))
            set.AddRange(library.ForPattern(pattern).Take(perPattern));
        return set;
    }
}
