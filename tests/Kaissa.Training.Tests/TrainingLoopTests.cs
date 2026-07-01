using Kaissa.Chess.Rules;
using Kaissa.Learning;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class TrainingLoopTests
{
    private static readonly DateTime Origin = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Default_library_loads_and_every_scenario_is_legal_content()
    {
        var library = ScenarioLibrary.LoadDefault();

        Assert.NotEmpty(library.Patterns);
        Assert.NotEmpty(library.AllScenarios);

        foreach (var scenario in library.AllScenarios)
        {
            var game = ChessGame.FromFen(scenario.Fen); // throws if the FEN is invalid
            var legal = game.LegalUciMoves();

            Assert.NotEmpty(scenario.Solutions);
            foreach (var solution in scenario.Solutions)
                Assert.Contains(solution, legal);

            Assert.Contains(scenario.Pattern, library.Patterns);
        }
    }

    [Theory]
    [InlineData(2, true, Rating.Easy)]   // correct and fast
    [InlineData(7, true, Rating.Good)]   // correct, moderate
    [InlineData(30, true, Rating.Hard)]  // correct but slow
    public void Grade_reflects_speed_of_a_correct_move(int seconds, bool expectCorrect, Rating expectRating)
    {
        var library = ScenarioLibrary.LoadDefault();
        var scenario = library.AllScenarios.First();
        var grader = new GradeExtractor();

        var attempt = grader.Grade(scenario, scenario.Solutions[0], TimeSpan.FromSeconds(seconds));

        Assert.Equal(expectCorrect, attempt.Correct);
        Assert.Equal(expectRating, attempt.Rating);
    }

    [Fact]
    public void Wrong_move_is_graded_as_a_lapse()
    {
        var library = ScenarioLibrary.LoadDefault();
        var scenario = library.AllScenarios.First();
        var grader = new GradeExtractor();

        // A legal but non-solution move (the king step) for the back-rank position.
        var attempt = grader.Grade(scenario, "h1g1", TimeSpan.FromSeconds(5));

        Assert.False(attempt.Correct);
        Assert.Equal(Rating.Again, attempt.Rating);
    }

    [Fact]
    public void Submit_before_next_throws()
    {
        var session = NewSession(out _);
        Assert.Throws<InvalidOperationException>(() => session.Submit("e2e4", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void A_correct_answer_schedules_the_pattern_into_the_future()
    {
        var session = NewSession(out var clock);

        var scenario = session.Next();
        Assert.NotNull(scenario);

        var outcome = session.Submit(scenario!.Solutions[0], TimeSpan.FromSeconds(5));

        Assert.True(outcome.Correct);
        Assert.True(outcome.IntervalDays >= 1);
        Assert.True(outcome.DueUtc > clock.UtcNow);
    }

    [Fact]
    public void New_patterns_are_introduced_before_any_are_repeated()
    {
        var library = ScenarioLibrary.LoadDefault();
        var model = new SkillModel();
        var clock = new ManualClock(Origin);
        var session = new TrainingSession(library, model, clock);

        var introduced = new HashSet<PatternId>();
        for (int i = 0; i < library.Patterns.Count; i++)
        {
            var scenario = session.Next();
            Assert.NotNull(scenario);
            Assert.True(introduced.Add(scenario!.Pattern), "A pattern repeated before all were introduced.");
            session.Submit(scenario.Solutions[0], TimeSpan.FromSeconds(5));
        }

        Assert.Equal(library.Patterns.Count, introduced.Count);
    }

    [Fact]
    public void A_single_pattern_matures_with_correctly_spaced_practice()
    {
        // Isolate one pattern so the planner cannot switch away, and always answer it correctly,
        // each time jumping forward to its scheduled review. Intervals should lengthen and the
        // memory should become durable — the core promise of the spaced-repetition loop.
        var full = ScenarioLibrary.LoadDefault();
        var pattern = new PatternId("checkmate.back_rank");
        var library = new ScenarioLibrary(new[] { full.Describe(pattern) }, full.ForPattern(pattern));

        var model = new SkillModel();
        var clock = new ManualClock(Origin);
        var session = new TrainingSession(library, model, clock);

        var intervals = new List<int>();
        for (int i = 0; i < 12; i++)
        {
            var scenario = session.Next();
            Assert.NotNull(scenario);
            var outcome = session.Submit(scenario!.Solutions[0], TimeSpan.FromSeconds(6)); // "Good"
            intervals.Add(outcome.IntervalDays);
            clock.Advance(outcome.DueUtc - clock.UtcNow); // advance to the scheduled review
        }

        Assert.True(intervals[^1] > intervals[0], $"intervals did not grow: [{string.Join(", ", intervals)}]");
        var stability = model.GetOrCreate(pattern).State!.Value.Stability;
        Assert.True(stability > 15, $"stability was {stability:0.0}");
    }

    [Fact]
    public void A_diligent_learner_covers_and_reinforces_every_pattern()
    {
        var library = ScenarioLibrary.LoadDefault();
        var model = new SkillModel();
        var clock = new ManualClock(Origin);
        var session = new TrainingSession(library, model, clock);

        // Steady study: one item at a time, a few days apart, always answered correctly.
        for (int i = 0; i < 40; i++)
        {
            var scenario = session.Next();
            Assert.NotNull(scenario);
            session.Submit(scenario!.Solutions[0], TimeSpan.FromSeconds(6));
            clock.AdvanceDays(3);
        }

        Assert.All(library.Patterns, p => Assert.True(model.Has(p), $"pattern never introduced: {p}"));
        Assert.All(model.Cards, c => Assert.True(c.Reps >= 1));
        var maxStability = model.Cards.Max(c => c.State!.Value.Stability);
        Assert.True(maxStability > FsrsInitialGoodStability, $"no growth: max stability {maxStability:0.0}");
    }

    private const double FsrsInitialGoodStability = 2.3065;

    private static TrainingSession NewSession(out ManualClock clock)
    {
        clock = new ManualClock(Origin);
        return new TrainingSession(ScenarioLibrary.LoadDefault(), new SkillModel(), clock);
    }
}
