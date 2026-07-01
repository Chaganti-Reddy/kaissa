using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class AdaptiveTests
{
    private static readonly DateTime Origin = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Expected_score_is_half_at_equal_ratings()
    {
        Assert.Equal(0.5, RatingEstimator.ExpectedScore(1000, 1000), 6);
    }

    [Fact]
    public void Solving_harder_puzzles_raises_the_rating_more_than_easy_ones()
    {
        var gainHard = RatingEstimator.Update(1000, 1400, solved: true) - 1000;
        var gainEasy = RatingEstimator.Update(1000, 600, solved: true) - 1000;
        Assert.True(gainHard > gainEasy);
        Assert.True(gainEasy > 0);
    }

    [Fact]
    public void Failing_easy_puzzles_drops_the_rating_more_than_hard_ones()
    {
        var dropEasy = 1000 - RatingEstimator.Update(1000, 600, solved: false);
        var dropHard = 1000 - RatingEstimator.Update(1000, 1400, solved: false);
        Assert.True(dropEasy > dropHard);
        Assert.True(dropHard > 0);
    }

    [Fact]
    public void Difficulty_controller_picks_the_puzzle_nearest_the_target_level()
    {
        var scenarios = new[]
        {
            Scn("easy", 400), Scn("mid", 900), Scn("hard", 1500),
        };
        var controller = new DifficultyController(targetOffset: 50);

        // Player 850 -> target 900 -> the "mid" puzzle.
        Assert.Equal("mid", controller.Pick(scenarios, playerRating: 850).Id);
        // Avoiding "mid" falls back to the next-closest.
        Assert.NotEqual("mid", controller.Pick(scenarios, playerRating: 850, avoidId: "mid").Id);
    }

    [Fact]
    public void A_correct_answer_raises_the_player_rating_and_a_wrong_one_lowers_it()
    {
        var library = ScenarioLibrary.LoadDefault();
        var model = new SkillModel();
        var session = new TrainingSession(library, model, new ManualClock(Origin));

        var start = model.RatingEstimate;
        var scenario = session.Next()!;
        var solved = session.Submit(scenario.Solutions[0], TimeSpan.FromSeconds(5));
        Assert.True(solved.PlayerRating > start);

        var afterSolve = model.RatingEstimate;
        session.Next();
        var failed = session.Submit("0000", TimeSpan.FromSeconds(5)); // unresolvable -> wrong
        Assert.True(failed.PlayerRating < afterSolve);
    }

    [Fact]
    public void The_rating_estimate_converges_toward_a_learners_true_skill()
    {
        const double trueSkill = 1200;
        var library = ScenarioLibrary.LoadDefault();
        var model = new SkillModel();
        var clock = new ManualClock(Origin);
        var session = new TrainingSession(library, model, clock);
        var rng = new Random(42);

        for (int i = 0; i < 300; i++)
        {
            var scenario = session.Next()!;
            bool solves = rng.NextDouble() < RatingEstimator.ExpectedScore(trueSkill, scenario.Rating);
            session.Submit(solves ? scenario.Solutions[0] : "0000", TimeSpan.FromSeconds(5));
            clock.AdvanceDays(1);
        }

        Assert.InRange(model.RatingEstimate, trueSkill - 250, trueSkill + 250);
    }

    private static Scenario Scn(string id, int rating) =>
        new(id, new PatternId("tactic.fork"), "8/8/8/8/8/8/8/8 w - - 0 1", new[] { "a1a2" }, "test", rating);
}
