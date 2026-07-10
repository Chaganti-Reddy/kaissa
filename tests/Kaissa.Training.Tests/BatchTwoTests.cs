using Kaissa.Chess.Rules;
using Kaissa.Learning;
using Kaissa.Training;
using Kaissa.Training.Api;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class BatchTwoTests
{
    // --- Themed practice ---

    [Fact]
    public void Themed_session_serves_only_the_chosen_pattern()
    {
        var library = ScenarioLibrary.LoadDefault();
        var pattern = new PatternId("checkmate.back_rank");
        var session = new ThemedSession(library, pattern, startRating: 900);

        for (int i = 0; i < 6; i++)
        {
            var scenario = session.Next();
            Assert.Equal(pattern, scenario.Pattern);
            var result = session.Submit(scenario.Solutions[0], TimeSpan.FromSeconds(3));
            Assert.True(result.Correct);
        }

        Assert.Equal(6, session.Score);
        Assert.Equal(6, session.Attempts);
    }

    // --- Weakness report ---

    [Fact]
    public void Weakness_report_ranks_least_stable_patterns_first()
    {
        var model = new SkillModel();
        SetStability(model, "tactic.fork", 40);
        SetStability(model, "tactic.pin", 5);
        SetStability(model, "checkmate.back_rank", 20);

        var weakest = WeaknessReport.WeakestPatterns(model, 2);

        Assert.Equal(new PatternId("tactic.pin"), weakest[0]);          // least stable
        Assert.Equal(new PatternId("checkmate.back_rank"), weakest[1]);
    }

    [Fact]
    public void Weakness_practice_set_is_drawn_from_weak_patterns()
    {
        var library = ScenarioLibrary.LoadDefault();
        var model = new SkillModel();
        SetStability(model, "tactic.pin", 5);
        SetStability(model, "tactic.skewer", 8);

        var set = WeaknessReport.BuildPracticeSet(model, library, patternCount: 2, perPattern: 3);

        Assert.NotEmpty(set);
        Assert.All(set, s => Assert.Contains(s.Pattern.Value, new[] { "tactic.pin", "tactic.skewer" }));
    }

    // --- Endgame library ---

    [Fact]
    public void Endgame_positions_are_legal_and_playable()
    {
        Assert.NotEmpty(EndgameLibrary.All);
        foreach (var endgame in EndgameLibrary.All)
        {
            var game = ChessGame.FromFen(endgame.Fen); // throws if invalid
            Assert.False(game.IsGameOver, $"{endgame.Id} should not be finished");
            Assert.NotEmpty(game.LegalUciMoves());
        }

        Assert.NotNull(EndgameLibrary.ById("mate_kq"));
        Assert.Null(EndgameLibrary.ById("nope"));
    }

    // --- Analysis (gated on an engine) ---

    [Fact]
    public async Task Analysis_evaluates_a_position()
    {
        var path = Environment.GetEnvironmentVariable("KAISSA_STOCKFISH_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        await using var analysis = await KaissaAnalysis.StartAsync(path);
        var line = await analysis.EvaluateAsync("startpos", depth: 12);

        Assert.InRange(line.BestMove.Length, 4, 5);
        Assert.False(string.IsNullOrEmpty(line.Score));
    }

    private static void SetStability(SkillModel model, string pattern, double stability)
    {
        var card = model.GetOrCreate(new PatternId(pattern));
        card.State = new MemoryState(stability, 5.0);
        card.Reps = 3;
    }
}
