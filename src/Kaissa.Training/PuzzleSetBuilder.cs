using System.Collections.Generic;
using System.Linq;

namespace Kaissa.Training;

/// <summary>A filter describing a custom puzzle set: a rating band, optional pattern ids and theme tags
/// (any-match), whether to keep only single-move puzzles, and a cap. Any unset filter matches everything.</summary>
public sealed class PuzzleSetSpec
{
    public int MinRating { get; init; } = 0;
    public int MaxRating { get; init; } = 4000;
    public IReadOnlyCollection<string>? Patterns { get; init; }
    public IReadOnlyCollection<string>? Themes { get; init; }
    public bool SingleMoveOnly { get; init; }
    public int Max { get; init; } = 50;
}

/// <summary>
/// Builds a custom problem set from the library by combining filters (rating band, patterns, themes,
/// single-move) - the Chesstempo-style set builder, pure and offline. Deterministic order (by rating then
/// id) so the same spec yields the same set.
/// </summary>
public static class PuzzleSetBuilder
{
    public static IReadOnlyList<Scenario> Build(ScenarioLibrary library, PuzzleSetSpec spec)
    {
        int lo = System.Math.Min(spec.MinRating, spec.MaxRating);
        int hi = System.Math.Max(spec.MinRating, spec.MaxRating);

        IEnumerable<Scenario> q = library.AllScenarios.Where(s => s.Rating >= lo && s.Rating <= hi);

        if (spec.Patterns is { Count: > 0 })
        {
            var set = new HashSet<string>(spec.Patterns);
            q = q.Where(s => set.Contains(s.Pattern.Value));
        }
        if (spec.Themes is { Count: > 0 })
        {
            var set = new HashSet<string>(spec.Themes);
            q = q.Where(s => s.ThemeTags.Any(set.Contains));
        }
        if (spec.SingleMoveOnly)
            q = q.Where(s => s.IsSingleMove);

        return q.OrderBy(s => s.Rating).ThenBy(s => s.Id)
            .Take(System.Math.Max(0, spec.Max))
            .ToList();
    }
}
