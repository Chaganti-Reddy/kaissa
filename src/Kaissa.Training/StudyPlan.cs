using System.Collections.Generic;
using System.Linq;
using Kaissa.Training.Play;

namespace Kaissa.Training;

/// <summary>One suggestion in the weekly study plan: what to work on, why, and where to do it.</summary>
public sealed record StudyPlanItem(string Title, string Reason, string Route);

/// <summary>
/// Builds a short, prioritized week's plan from the player's own weakness dashboard and weakest patterns
/// - the axes furthest behind their peer baseline come first, each pointing at the matching drill. Pure
/// and deterministic (no engine); when nothing is behind, it suggests keeping the strongest areas sharp.
/// </summary>
public static class StudyPlan
{
    public static IReadOnlyList<StudyPlanItem> Generate(
        IReadOnlyList<AxisScore> axes, IReadOnlyList<string> weakestPatterns, int max = 4)
    {
        var items = new List<StudyPlanItem>();

        // Axes that are behind their peer baseline, worst gap first.
        foreach (var a in axes.Where(x => x.HasData && x.Score < x.Peer).OrderBy(x => x.Score - x.Peer))
            items.Add(new StudyPlanItem(
                $"Work on {a.Name.ToLowerInvariant()}",
                a.Line,
                a.DrillRoute ?? "SampleScene"));

        // Then the single weakest pattern, as a targeted tactic session.
        var pattern = weakestPatterns?.FirstOrDefault();
        if (!string.IsNullOrEmpty(pattern))
            items.Add(new StudyPlanItem($"Drill {pattern}", "Your least-stable pattern in the spaced schedule.", "SampleScene"));

        if (items.Count == 0)
            items.Add(new StudyPlanItem("Keep your edge", "No clear weakness right now - keep the strongest areas sharp and play.", "Play"));

        return items.Take(max).ToList();
    }
}
