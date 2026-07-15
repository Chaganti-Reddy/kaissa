using System.Linq;
using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public class WeaknessDashboardTests
{
    private static AxisScore Axis(WeaknessInput input, string name) =>
        WeaknessDashboard.Compute(input).Single(a => a.Name == name);

    [Fact]
    public void Reports_all_six_axes()
    {
        var axes = WeaknessDashboard.Compute(new WeaknessInput());
        Assert.Equal(6, axes.Count);
        Assert.Contains(axes, a => a.Name == "Tactics");
        Assert.Contains(axes, a => a.Name == "Endgame");
        Assert.Contains(axes, a => a.Name == "Advantage capitalization");
        Assert.Contains(axes, a => a.Name == "Resourcefulness");
        Assert.Contains(axes, a => a.Name == "Time management");
        Assert.Contains(axes, a => a.Name == "Opening");
    }

    [Fact]
    public void Axis_with_no_games_has_no_data()
    {
        var tactics = Axis(new WeaknessInput(), "Tactics");
        Assert.False(tactics.HasData);
        Assert.Equal("none", tactics.Verdict);
        Assert.Contains("Not enough games", tactics.Line);
    }

    [Fact]
    public void Tactics_score_is_found_over_total()
    {
        var tactics = Axis(new WeaknessInput { TacticsFound = 6, TacticsMissed = 2 }, "Tactics");
        Assert.True(tactics.HasData);
        Assert.Equal(75, tactics.Score); // 6 / 8
    }

    [Fact]
    public void Advantage_and_resourcefulness_are_ratios_of_their_games()
    {
        var adv = Axis(new WeaknessInput { AdvantageGames = 4, AdvantageConverted = 3 }, "Advantage capitalization");
        Assert.Equal(75, adv.Score);

        var res = Axis(new WeaknessInput { LosingGames = 5, LosingSaved = 1 }, "Resourcefulness");
        Assert.Equal(20, res.Score);
    }

    [Fact]
    public void Time_management_is_share_of_clock_left()
    {
        var t = Axis(new WeaknessInput { TimedGames = 3, TimeClockShare = 0.4 }, "Time management");
        Assert.Equal(40, t.Score);
    }

    [Fact]
    public void Phase_accuracy_maps_straight_to_its_axis()
    {
        var input = new WeaknessInput { OpeningAccuracy = 88, EndgameAccuracy = 51 };
        Assert.Equal(88, Axis(input, "Opening").Score);
        Assert.Equal(51, Axis(input, "Endgame").Score);
    }

    [Fact]
    public void Verdict_reflects_position_against_the_peer_baseline()
    {
        // Low rating -> low tactics baseline; a strong tactics score should read "ahead".
        var ahead = Axis(new WeaknessInput { Rating = 800, TacticsFound = 95, TacticsMissed = 5 }, "Tactics");
        Assert.Equal("ahead", ahead.Verdict);

        var behind = Axis(new WeaknessInput { Rating = 800, TacticsFound = 1, TacticsMissed = 9 }, "Tactics");
        Assert.Equal("behind", behind.Verdict);
        Assert.Contains("typical at your level", behind.Line);
    }

    [Fact]
    public void Peer_baselines_rise_with_rating()
    {
        var low = Axis(new WeaknessInput { Rating = 800, OpeningAccuracy = 70 }, "Opening");
        var high = Axis(new WeaknessInput { Rating = 2000, OpeningAccuracy = 70 }, "Opening");
        Assert.True(high.Peer > low.Peer);
    }

    [Fact]
    public void WeakestActionable_picks_the_furthest_behind()
    {
        var input = new WeaknessInput
        {
            Rating = 1200,
            TacticsFound = 1, TacticsMissed = 9,   // ~10%, far behind
            OpeningAccuracy = 85,                   // likely ahead
            EndgameAccuracy = 80,
        };
        var axes = WeaknessDashboard.Compute(input);
        var weakest = WeaknessDashboard.WeakestActionable(axes);
        Assert.NotNull(weakest);
        Assert.Equal("Tactics", weakest!.Name);
    }

    [Fact]
    public void WeakestActionable_is_null_when_no_data()
    {
        var axes = WeaknessDashboard.Compute(new WeaknessInput());
        Assert.Null(WeaknessDashboard.WeakestActionable(axes));
    }

    [Fact]
    public void Scores_are_clamped_to_0_100()
    {
        var axes = WeaknessDashboard.Compute(new WeaknessInput
        {
            OpeningAccuracy = 130, EndgameAccuracy = -20, TimedGames = 1, TimeClockShare = 2.0,
        });
        Assert.All(axes, a => Assert.InRange(a.Score, 0, 100));
        Assert.All(axes, a => Assert.InRange(a.Peer, 0, 100));
    }
}
