using Kaissa.Chess.Engine;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

/// <summary>
/// Confirms the bundled scenarios are tactically sound: for each one the engine's best move is an
/// accepted solution. Runs only when KAISSA_STOCKFISH_PATH is set, so CI without an engine skips it.
/// This is content quality assurance — a wrong FEN or solution fails the build.
/// </summary>
public sealed class ScenarioSoundnessTests
{
    private static string? EnginePath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("KAISSA_STOCKFISH_PATH");
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
        }
    }

    [Fact]
    public async Task Every_scenario_solution_is_the_engines_best_move()
    {
        var path = EnginePath;
        if (path is null)
            return; // No engine available; skip.

        var library = ScenarioLibrary.LoadDefault();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await using var engine = UciChessEngine.LaunchProcess(path);
        await engine.HandshakeAsync(cts.Token);

        foreach (var scenario in library.AllScenarios)
        {
            await engine.NewGameAsync(cts.Token);
            var result = await engine.AnalyzeAsync(scenario.Fen, SearchLimits.ToDepth(18), cts.Token);

            Assert.True(
                scenario.Solutions.Contains(result.BestMove, StringComparer.OrdinalIgnoreCase),
                $"Scenario '{scenario.Id}': engine preferred '{result.BestMove}', not in [{string.Join(", ", scenario.Solutions)}].");
        }
    }
}
