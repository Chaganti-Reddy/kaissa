using Kaissa.Chess.Engine;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

/// <summary>
/// Confirms the bundled scenarios are tactically sound: the accepted solution is among the
/// engine's best moves. Runs only when KAISSA_STOCKFISH_PATH is set, so CI without an engine
/// skips it. A deterministic sample keeps it fast while still catching bad content.
/// </summary>
public sealed class ScenarioSoundnessTests
{
    private const int SampleSize = 20;

    private static string? EnginePath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("KAISSA_STOCKFISH_PATH");
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
        }
    }

    [Fact]
    public async Task Sampled_scenario_solutions_are_among_the_engines_best_moves()
    {
        var path = EnginePath;
        if (path is null)
            return; // No engine available; skip.

        var library = ScenarioLibrary.LoadDefault();
        var ordered = library.AllScenarios.OrderBy(s => s.Id).ToList();
        var step = Math.Max(1, ordered.Count / SampleSize);
        var sample = ordered.Where((_, i) => i % step == 0).Take(SampleSize).ToList();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var engine = UciChessEngine.LaunchProcess(path);
        await engine.HandshakeAsync(cts.Token);

        var failures = new List<string>();
        foreach (var scenario in sample)
        {
            await engine.NewGameAsync(cts.Token);
            var result = await engine.AnalyzeAsync(scenario.Fen, SearchLimits.ToDepth(16, multiPv: 3), cts.Token);

            // The solution should be one of the engine's top few candidate moves.
            var topMoves = result.Lines.Select(l => l.BestMove).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (result.BestMove.Length > 0)
                topMoves.Add(result.BestMove);

            if (!scenario.Solutions.Any(topMoves.Contains))
                failures.Add($"{scenario.Id}: solution [{string.Join(",", scenario.Solutions)}] not in top moves [{string.Join(",", topMoves)}]");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
