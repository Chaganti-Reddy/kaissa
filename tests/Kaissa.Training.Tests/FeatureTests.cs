using Kaissa.Training;
using Kaissa.Training.Api;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class FeatureTests
{
    private static readonly DateTime Origin = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // --- Daily puzzle ---

    [Fact]
    public void Daily_puzzle_is_stable_for_a_date_and_is_real_content()
    {
        var library = ScenarioLibrary.LoadDefault();
        var a = DailyPuzzle.ForDate(library, new DateTime(2026, 7, 1));
        var b = DailyPuzzle.ForDate(library, new DateTime(2026, 7, 1));

        Assert.Equal(a.Id, b.Id);
        Assert.Contains(library.AllScenarios, s => s.Id == a.Id);
    }

    // --- Board vision ---

    [Theory]
    [InlineData("a1", false)]
    [InlineData("h1", true)]
    [InlineData("e4", true)]
    [InlineData("h8", false)]
    [InlineData("a8", true)]
    public void Square_colour_is_correct(string square, bool expectLight)
    {
        Assert.Equal(expectLight, BoardVision.IsLightSquare(square));
    }

    [Fact]
    public void Vision_session_scores_only_correct_answers()
    {
        var vision = new VisionSession();

        var square1 = vision.NextSquare();
        Assert.True(vision.Answer(BoardVision.IsLightSquare(square1)));
        Assert.Equal(1, vision.Score);

        var square2 = vision.NextSquare();
        Assert.False(vision.Answer(!BoardVision.IsLightSquare(square2)));
        Assert.Equal(1, vision.Score);
        Assert.Equal(2, vision.Asked);
    }

    // --- Stats ---

    [Fact]
    public void Stats_track_attempts_accuracy_and_streaks()
    {
        var trainer = SingleScenarioTrainer();

        for (int i = 0; i < 5; i++)
        {
            trainer.NextCard();
            trainer.Answer("a1a8", TimeSpan.FromSeconds(3)); // correct
        }

        var stats = trainer.GetStats();
        Assert.Equal(5, stats.TotalAttempts);
        Assert.Equal(5, stats.TotalCorrect);
        Assert.Equal(1.0, stats.Accuracy, 6);
        Assert.Equal(5, stats.CurrentStreak);
        Assert.Equal(5, stats.BestStreak);
        Assert.Equal(5, stats.RatingHistory.Count);

        trainer.NextCard();
        trainer.Answer("h1g1", TimeSpan.FromSeconds(3)); // wrong

        var after = trainer.GetStats();
        Assert.Equal(6, after.TotalAttempts);
        Assert.Equal(5, after.TotalCorrect);
        Assert.Equal(0, after.CurrentStreak);
        Assert.Equal(5, after.BestStreak); // best preserved
    }

    private static KaissaTrainer SingleScenarioTrainer()
    {
        var pattern = new Pattern(new PatternId("checkmate.back_rank"), "Back-rank mate", "Mate on the back rank.");
        var scenario = new Scenario("s1", pattern.Id, "6k1/5ppp/8/8/8/8/8/R6K w - - 0 1",
            new[] { "a1a8" }, "White mates in one.", 900);
        var library = new ScenarioLibrary(new[] { pattern }, new[] { scenario });
        return new KaissaTrainer(library, new SkillModel(), new ManualClock(Origin));
    }
}
