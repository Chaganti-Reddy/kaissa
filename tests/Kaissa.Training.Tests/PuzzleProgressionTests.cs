using Kaissa.Training;
using Kaissa.Training.Api;
using Xunit;
using static Kaissa.Training.PuzzleProgression;

namespace Kaissa.Training.Tests;

public class PuzzleProgressionTests
{
    [Fact]
    public void Harder_puzzle_awards_more_xp_than_easier()
    {
        int easy = XpForSolve(800, 1200, hintUsed: false, firstTry: true, solveSeconds: 20, sessionSolveStreak: 0);
        int hard = XpForSolve(2000, 1200, hintUsed: false, firstTry: true, solveSeconds: 20, sessionSolveStreak: 0);
        Assert.True(hard > easy);
    }

    [Fact]
    public void Hint_sharply_reduces_xp()
    {
        int clean = XpForSolve(1400, 1400, hintUsed: false, firstTry: true, solveSeconds: 20, sessionSolveStreak: 0);
        int hinted = XpForSolve(1400, 1400, hintUsed: true, firstTry: false, solveSeconds: 20, sessionSolveStreak: 0);
        Assert.True(hinted < clean / 2);
    }

    [Fact]
    public void Session_streak_boosts_xp_up_to_a_cap()
    {
        int none = XpForSolve(1400, 1400, false, true, 20, 0);
        int five = XpForSolve(1400, 1400, false, true, 20, 5);
        int ten = XpForSolve(1400, 1400, false, true, 20, 10);
        int twenty = XpForSolve(1400, 1400, false, true, 20, 20);
        Assert.True(five > none);
        Assert.True(ten > five);
        Assert.Equal(ten, twenty); // capped at +50% (reached by 10)
    }

    [Fact]
    public void Tier_standing_advances_with_xp()
    {
        Assert.Equal("Wood", Standing(0).Name);
        Assert.Equal("Bronze", Standing(1_500).Name);
        var d = Standing(60_000);
        Assert.Equal("Diamond", d.Name);
        Assert.False(d.IsMax);
    }

    [Fact]
    public void Top_tier_is_maxed()
    {
        var gm = Standing(500_000);
        Assert.Equal("Grandmaster", gm.Name);
        Assert.True(gm.IsMax);
        Assert.Equal(1f, gm.Fraction);
    }

    [Fact]
    public void Tier_fraction_is_within_the_current_band()
    {
        var s = Standing(2_750); // halfway between Bronze(1500) and Silver(4000)
        Assert.Equal("Bronze", s.Name);
        Assert.InRange(s.Fraction, 0.49f, 0.51f);
    }

    [Fact]
    public void Unseen_pattern_is_unseen()
    {
        var row = new ProgressRow("tactic.fork", "Fork", Seen: false, Reps: 0, Lapses: 0, StabilityDays: 0);
        Assert.Equal(Mastery.Unseen, MasteryFor(row));
    }

    [Fact]
    public void High_stability_low_lapses_is_mastered()
    {
        var row = new ProgressRow("tactic.fork", "Fork", Seen: true, Reps: 20, Lapses: 1, StabilityDays: 200);
        Assert.Equal(Mastery.Mastered, MasteryFor(row));
    }

    [Fact]
    public void Many_lapses_cap_mastery_below_strong()
    {
        var row = new ProgressRow("tactic.fork", "Fork", Seen: true, Reps: 10, Lapses: 6, StabilityDays: 200);
        Assert.Equal(Mastery.Proficient, MasteryFor(row));
    }

    [Fact]
    public void Fresh_pattern_is_learning()
    {
        var row = new ProgressRow("tactic.fork", "Fork", Seen: true, Reps: 2, Lapses: 0, StabilityDays: 1);
        Assert.Equal(Mastery.Learning, MasteryFor(row));
    }
}
