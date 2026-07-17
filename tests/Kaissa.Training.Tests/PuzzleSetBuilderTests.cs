using System.Linq;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class PuzzleSetBuilderTests
{
    private static readonly ScenarioLibrary Lib = ScenarioLibrary.LoadDefault();

    [Fact]
    public void Rating_band_is_respected()
    {
        var set = PuzzleSetBuilder.Build(Lib, new PuzzleSetSpec { MinRating = 900, MaxRating = 1100, Max = 200 });
        Assert.NotEmpty(set);
        Assert.All(set, s => Assert.InRange(s.Rating, 900, 1100));
    }

    [Fact]
    public void Pattern_filter_returns_only_that_pattern()
    {
        var pattern = Lib.Patterns.First().Value;
        var set = PuzzleSetBuilder.Build(Lib, new PuzzleSetSpec { Patterns = new[] { pattern }, Max = 100 });
        Assert.NotEmpty(set);
        Assert.All(set, s => Assert.Equal(pattern, s.Pattern.Value));
    }

    [Fact]
    public void Cap_and_determinism_hold()
    {
        var spec = new PuzzleSetSpec { Max = 12 };
        var a = PuzzleSetBuilder.Build(Lib, spec);
        var b = PuzzleSetBuilder.Build(Lib, spec);
        Assert.True(a.Count <= 12);
        Assert.Equal(a.Select(s => s.Id), b.Select(s => s.Id)); // same spec -> same set, same order
    }

    [Fact]
    public void Single_move_filter_keeps_only_single_move_puzzles()
    {
        var set = PuzzleSetBuilder.Build(Lib, new PuzzleSetSpec { SingleMoveOnly = true, Max = 50 });
        Assert.All(set, s => Assert.True(s.IsSingleMove));
    }

    [Fact]
    public void Empty_result_when_nothing_matches()
    {
        var set = PuzzleSetBuilder.Build(Lib, new PuzzleSetSpec { MinRating = 9000, MaxRating = 9999 });
        Assert.Empty(set);
    }
}
