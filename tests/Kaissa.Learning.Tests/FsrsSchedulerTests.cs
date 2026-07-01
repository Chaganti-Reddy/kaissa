using Kaissa.Learning;
using Xunit;

namespace Kaissa.Learning.Tests;

public sealed class FsrsSchedulerTests
{
    private static readonly FsrsScheduler Scheduler = new();

    [Fact]
    public void Default_parameters_have_21_weights_and_derived_constants()
    {
        var p = new FsrsParameters();
        Assert.Equal(21, p.Weights.Count);
        Assert.Equal(-0.1542, p.Decay, 6);
        Assert.Equal(Math.Pow(0.9, 1.0 / p.Decay) - 1.0, p.Factor, 12);
    }

    [Theory]
    [InlineData(Rating.Again, 0.212)]
    [InlineData(Rating.Hard, 1.2931)]
    [InlineData(Rating.Good, 2.3065)]
    [InlineData(Rating.Easy, 8.2956)]
    public void Initial_stability_equals_the_first_rating_weight(Rating rating, double expected)
    {
        Assert.Equal(expected, Scheduler.InitialState(rating).Stability, 6);
    }

    [Fact]
    public void Initial_difficulty_matches_formula_and_clamps()
    {
        // Good: w4 - e^(w5*(3-1)) + 1 ≈ 2.118
        Assert.Equal(2.118, Scheduler.InitialState(Rating.Good).Difficulty, 2);
        // Easy: formula is strongly negative, so it clamps to the 1.0 floor.
        Assert.Equal(1.0, Scheduler.InitialState(Rating.Easy).Difficulty, 6);
    }

    [Fact]
    public void Retrievability_is_one_at_zero_and_equals_target_at_elapsed_equal_to_stability()
    {
        var state = new MemoryState(Stability: 10.0, Difficulty: 5.0);

        Assert.Equal(1.0, Scheduler.Retrievability(state, 0), 12);

        // By construction, recall probability after `stability` days is exactly the target retention.
        Assert.Equal(0.9, Scheduler.Retrievability(state, state.Stability), 9);
    }

    [Fact]
    public void Retrievability_decreases_as_time_passes()
    {
        var state = new MemoryState(10.0, 5.0);
        Assert.True(Scheduler.Retrievability(state, 5) > Scheduler.Retrievability(state, 20));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.3065)]
    [InlineData(10.0)]
    [InlineData(36.5)]
    public void Interval_at_default_retention_equals_rounded_stability(double stability)
    {
        Assert.Equal((int)Math.Round(stability), Scheduler.NextIntervalDays(stability));
    }

    [Fact]
    public void Higher_target_retention_yields_shorter_intervals()
    {
        var relaxed = new FsrsScheduler(new FsrsParameters(desiredRetention: 0.90));
        var strict = new FsrsScheduler(new FsrsParameters(desiredRetention: 0.97));
        Assert.True(strict.NextIntervalDays(20.0) < relaxed.NextIntervalDays(20.0));
    }

    [Fact]
    public void Successful_recall_increases_stability()
    {
        var state = Scheduler.InitialState(Rating.Good);
        var next = Scheduler.NextState(state, elapsedDays: 2, Rating.Good);
        Assert.True(next.Stability > state.Stability);
    }

    [Fact]
    public void Lapse_reduces_stability()
    {
        var state = new MemoryState(20.0, 5.0);
        var next = Scheduler.NextState(state, elapsedDays: 20, Rating.Again);
        Assert.True(next.Stability < state.Stability);
    }

    [Fact]
    public void Failing_makes_a_pattern_harder_than_acing_it()
    {
        var state = new MemoryState(10.0, 5.0);
        var afterAgain = Scheduler.NextState(state, 10, Rating.Again);
        var afterEasy = Scheduler.NextState(state, 10, Rating.Easy);
        Assert.True(afterAgain.Difficulty > afterEasy.Difficulty);
    }

    [Fact]
    public void Repeated_success_grows_stability_and_interval()
    {
        MemoryState? state = null;
        int interval = 0;
        double previousStability = 0;
        int previousInterval = 0;

        for (int i = 0; i < 5; i++)
        {
            var result = Scheduler.Review(state, elapsedDays: interval, Rating.Good);

            Assert.True(result.State.Stability >= previousStability);
            Assert.True(result.IntervalDays >= previousInterval);

            previousStability = result.State.Stability;
            previousInterval = result.IntervalDays;
            state = result.State;
            interval = result.IntervalDays;
        }

        // After several correct, well-spaced reviews the pattern is far more durable than at first.
        Assert.True(previousStability > Scheduler.InitialState(Rating.Good).Stability);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(0)]
    public void Invalid_weight_count_is_rejected(int count)
    {
        Assert.Throws<ArgumentException>(() => new FsrsParameters(new double[count]));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void Invalid_retention_is_rejected(double retention)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FsrsParameters(desiredRetention: retention));
    }
}
