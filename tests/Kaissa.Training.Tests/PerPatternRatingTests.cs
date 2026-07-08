using System;
using System.Collections.Generic;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class PerPatternRatingTests
{
    private static readonly DateTime Origin = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string Fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private static ScenarioLibrary TwoPatternLibrary()
    {
        var fork = new Pattern(new PatternId("tactic.fork"), "Fork", "d");
        var pin = new Pattern(new PatternId("tactic.pin"), "Pin", "d");
        var scenarios = new List<Scenario>();
        foreach (var p in new[] { "tactic.fork", "tactic.pin" })
            foreach (var rating in new[] { 700, 900, 1100, 1300, 1500 })
                scenarios.Add(new Scenario($"{p}-{rating}", new PatternId(p), Fen, new[] { "e2e4" }, "", rating));
        return new ScenarioLibrary(new[] { fork, pin }, scenarios);
    }

    [Fact]
    public void An_unseen_pattern_reports_the_overall_rating()
    {
        var model = new SkillModel { RatingEstimate = 1234 };
        Assert.Equal(1234, model.PatternRating(new PatternId("tactic.fork")));
    }

    [Fact]
    public void Ratings_diverge_per_pattern_when_the_player_is_stronger_at_one()
    {
        var model = new SkillModel();
        var session = new TrainingSession(TwoPatternLibrary(), model, new ManualClock(Origin));

        // Solve every fork, miss every pin.
        for (int i = 0; i < 30; i++)
        {
            var s = session.Next();
            if (s is null)
                break;
            bool isFork = s.Pattern.Value == "tactic.fork";
            session.Submit(isFork ? "e2e4" : "0000", TimeSpan.FromSeconds(3));
        }

        double fork = model.PatternRating(new PatternId("tactic.fork"));
        double pin = model.PatternRating(new PatternId("tactic.pin"));
        Assert.True(fork > pin, $"expected fork {fork} > pin {pin}");
    }

    [Fact]
    public void Per_pattern_rating_survives_a_save_load_round_trip()
    {
        var model = new SkillModel();
        var card = model.GetOrCreate(new PatternId("tactic.fork"));
        card.State = null;             // will be set by a real review; here we just persist the rating
        card.Rating = 1450;
        card.Reps = 1;

        var reloaded = SkillModel.FromJson(model.ToJson());
        Assert.Equal(1450, reloaded.GetOrCreate(new PatternId("tactic.fork")).Rating);
    }

    [Fact]
    public void A_save_from_before_per_pattern_ratings_falls_back_to_the_overall_rating()
    {
        // A saved model whose card JSON has no "Rating" field (pre-feature save).
        string legacyJson =
            "{\"RatingEstimate\":1320,\"CurrentStreak\":0,\"BestStreak\":0,\"RatingHistory\":[]," +
            "\"Cards\":[{\"Pattern\":\"tactic.fork\",\"Stability\":5.0,\"Difficulty\":5.0," +
            "\"LastReviewUtc\":null,\"DueUtc\":null,\"Reps\":3,\"Lapses\":0}]}";

        var model = SkillModel.FromJson(legacyJson);
        Assert.Equal(1320, model.GetOrCreate(new PatternId("tactic.fork")).Rating);
    }
}
