using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class RushSessionTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromSeconds(2);

    [Fact]
    public void Starts_with_lives_and_a_first_puzzle()
    {
        var rush = new RushSession(ScenarioLibrary.LoadDefault(), startRating: 800, lives: 3);
        Assert.Equal(3, rush.Lives);
        Assert.Equal(0, rush.Score);
        Assert.False(rush.IsOver);
        Assert.NotNull(rush.Next());
    }

    [Fact]
    public void Solving_raises_score_streak_and_difficulty()
    {
        var rush = new RushSession(ScenarioLibrary.LoadDefault(), startRating: 800, lives: 3);
        double startTarget = rush.TargetRating;

        for (int i = 0; i < 5; i++)
        {
            var scenario = rush.Next()!;
            var result = rush.Submit(scenario.Solutions[0], Fast);
            Assert.True(result.Correct);
        }

        Assert.Equal(5, rush.Score);
        Assert.Equal(5, rush.Streak);
        Assert.True(rush.TargetRating > startTarget); // difficulty ramped up
    }

    [Fact]
    public void Three_misses_end_the_run_and_reset_streak()
    {
        var rush = new RushSession(ScenarioLibrary.LoadDefault(), startRating: 800, lives: 3);

        // One solve first to build a streak, then miss it away.
        var first = rush.Next()!;
        Assert.True(rush.Submit(first.Solutions[0], Fast).Correct);
        Assert.Equal(1, rush.Streak);

        for (int i = 0; i < 3; i++)
        {
            rush.Next();
            var result = rush.Submit("0000", Fast); // illegal -> miss
            Assert.False(result.Correct);
            Assert.Equal(0, result.Streak);
        }

        Assert.True(rush.IsOver);
        Assert.Equal(0, rush.Lives);
        Assert.Null(rush.Next());
    }

    [Fact]
    public void Difficulty_ramp_serves_harder_puzzles_as_the_run_goes()
    {
        var rush = new RushSession(ScenarioLibrary.LoadDefault(), startRating: 700, lives: 99);

        var firstRating = rush.Next()!.Rating;
        for (int i = 0; i < 20; i++)
        {
            var scenario = rush.Next()!;
            rush.Submit(scenario.Solutions[0], Fast);
        }
        var laterRating = rush.Next()!.Rating;

        Assert.True(laterRating >= firstRating); // target climbed, so puzzles trend harder
    }
}
