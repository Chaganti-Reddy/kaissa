using Kaissa.Training;
using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class FusionTests
{
    private static readonly DateTime Origin = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string BackRankFen = "6k1/5ppp/8/8/8/8/8/R6K w - - 0 1";

    private static Scenario GamePracticeScenario(string id, string fen) =>
        new(id, GamePractice.Pattern.Id, fen, new[] { "a1a8" }, "You went wrong here.", 1200);

    [Fact]
    public void Library_add_registers_a_new_pattern_and_its_scenarios()
    {
        var library = ScenarioLibrary.LoadDefault();
        library.Add(GamePractice.Pattern, new[] { GamePracticeScenario("yourgame-1", BackRankFen) });

        Assert.Contains(GamePractice.Pattern.Id, library.Patterns);
        Assert.Single(library.ForPattern(GamePractice.Pattern.Id));
    }

    [Fact]
    public void Practice_store_round_trips_and_dedupes_by_position()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaissa-practice-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new PlayerPracticeStore();
            store.AddRange(new[]
            {
                GamePracticeScenario("yourgame-1", BackRankFen),
                GamePracticeScenario("yourgame-2", BackRankFen), // same position -> deduped
            });
            Assert.Equal(1, store.Count);

            store.Save(path);
            var reloaded = PlayerPracticeStore.Load(path);

            Assert.Equal(1, reloaded.Count);
            Assert.Equal(BackRankFen, reloaded.Scenarios.Single().Fen);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_game_derived_position_is_served_and_scheduled_in_the_training_loop()
    {
        var library = ScenarioLibrary.LoadDefault();
        library.Add(GamePractice.Pattern, new[] { GamePracticeScenario("yourgame-1", BackRankFen) });

        var model = new SkillModel();
        var session = new TrainingSession(library, model, new ManualClock(Origin));

        // The planner introduces patterns; drive the loop until the game-derived one is served.
        Scenario? served = null;
        for (int i = 0; i < library.Patterns.Count + 2 && served is null; i++)
        {
            var scenario = session.Next()!;
            if (scenario.Pattern == GamePractice.Pattern.Id)
                served = scenario;
            else
                session.Submit(scenario.Solutions[0], TimeSpan.FromSeconds(5));
        }

        Assert.NotNull(served);
        var outcome = session.Submit(served!.Solutions[0], TimeSpan.FromSeconds(5));
        Assert.True(outcome.Correct);
        Assert.True(model.Has(GamePractice.Pattern.Id));
    }
}
