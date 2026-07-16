using System.Linq;
using Kaissa.Training;
using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public class StudyPlanTests
{
    [Fact]
    public void Plan_leads_with_the_axis_that_is_furthest_behind()
    {
        var input = new WeaknessInput
        {
            Rating = 1200,
            TacticsFound = 1, TacticsMissed = 9,  // ~10% - far behind
            OpeningAccuracy = 85,                  // ahead
            EndgameAccuracy = 80,
            TimedGames = 4, TimeClockShare = 0.2,  // behind but less than tactics
        };
        var axes = WeaknessDashboard.Compute(input);
        var plan = StudyPlan.Generate(axes, new[] { "Fork" });
        Assert.NotEmpty(plan);
        Assert.Contains("tactics", plan[0].Title.ToLowerInvariant());
    }

    [Fact]
    public void With_no_weakness_the_plan_still_suggests_something()
    {
        var axes = WeaknessDashboard.Compute(new WeaknessInput()); // no data -> no axis is "behind"
        var plan = StudyPlan.Generate(axes, System.Array.Empty<string>());
        Assert.Single(plan);
        Assert.Equal("Keep your edge", plan[0].Title);
    }

    [Fact]
    public void Plan_is_capped()
    {
        var input = new WeaknessInput
        {
            Rating = 2000,
            TacticsFound = 1, TacticsMissed = 9,
            EndgameAccuracy = 10, OpeningAccuracy = 10,
            AdvantageGames = 5, AdvantageConverted = 0,
            LosingGames = 5, LosingSaved = 0,
            TimedGames = 5, TimeClockShare = 0.05,
        };
        var axes = WeaknessDashboard.Compute(input);
        var plan = StudyPlan.Generate(axes, new[] { "Pin", "Fork" }, max: 3);
        Assert.True(plan.Count <= 3);
    }
}
