using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class SrLevelTests
{
    [Fact]
    public void Unseen_items_are_level_zero()
    {
        Assert.Equal(0, SrLevel.Level(null, seen: false));
        Assert.Equal(0, SrLevel.Level(30, seen: false));
    }

    [Theory]
    [InlineData(1, 2)]     // >= 1d  -> level 2
    [InlineData(3, 3)]     // >= 3d  -> level 3
    [InlineData(7, 4)]     // >= 1wk -> level 4
    [InlineData(14, 5)]    // >= 2wk -> level 5
    [InlineData(30, 6)]    // >= 1mo -> level 6
    [InlineData(90, 7)]    // >= 3mo -> level 7
    [InlineData(180, 8)]   // >= 6mo -> level 8
    [InlineData(400, 8)]   // capped at 8
    public void Interval_maps_onto_the_expected_ladder_rung(int days, int expectedLevel)
    {
        Assert.Equal(expectedLevel, SrLevel.Level(days, seen: true));
    }

    [Fact]
    public void Level_rises_monotonically_with_interval()
    {
        int prev = -1;
        foreach (int d in new[] { 1, 2, 3, 6, 7, 20, 45, 120, 200 })
        {
            int lvl = SrLevel.Level(d, seen: true);
            Assert.True(lvl >= prev);
            prev = lvl;
        }
    }

    [Fact]
    public void Top_of_the_ladder_is_mastered()
    {
        Assert.True(SrLevel.IsMastered(SrLevel.Level(200, seen: true)));
        Assert.False(SrLevel.IsMastered(SrLevel.Level(3, seen: true)));
    }

    [Theory]
    [InlineData(2, "2d")]
    [InlineData(10, "1wk")]
    [InlineData(21, "3wk")]
    [InlineData(60, "2mo")]
    [InlineData(400, "1y")]
    public void Friendly_labels_read_naturally(int days, string expected)
    {
        Assert.Equal(expected, SrLevel.Friendly(days));
    }
}
