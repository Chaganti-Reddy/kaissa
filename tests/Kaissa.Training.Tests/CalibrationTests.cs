using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class CalibrationTests
{
    [Fact]
    public void Calibration_runs_a_fixed_number_of_puzzles()
    {
        var session = new CalibrationSession(ScenarioLibrary.LoadDefault(), puzzles: 8);
        Assert.Equal(8, session.Total);

        int served = 0;
        while (!session.IsComplete)
        {
            var scenario = session.Next()!;
            session.Submit(scenario.Solutions[0], TimeSpan.FromSeconds(4));
            served++;
        }

        Assert.Equal(8, served);
        Assert.True(session.IsComplete);
        Assert.Null(session.Next());
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1500)]
    [InlineData(1900)]
    public void Calibration_estimate_lands_near_a_players_true_skill(double trueSkill)
    {
        var session = new CalibrationSession(ScenarioLibrary.LoadDefault(), puzzles: 15, startRating: 1200);
        var rng = new Random(7);

        while (!session.IsComplete)
        {
            var scenario = session.Next()!;
            bool solves = rng.NextDouble() < RatingEstimator.ExpectedScore(trueSkill, scenario.Rating);
            session.Submit(solves ? scenario.Solutions[0] : "0000", TimeSpan.FromSeconds(4));
        }

        Assert.InRange(session.EstimatedRating, trueSkill - 350, trueSkill + 350);
    }
}
