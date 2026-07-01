using Kaissa.Chess.Engine;
using Xunit;

namespace Kaissa.Chess.Engine.Tests;

/// <summary>
/// Exercises <see cref="ProcessUciTransport"/> against a real engine. Runs only when
/// KAISSA_STOCKFISH_PATH points at an engine binary, so CI without one simply skips it.
/// </summary>
public sealed class StockfishIntegrationTests
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
    public async Task Real_engine_handshakes_and_finds_a_move()
    {
        var path = EnginePath;
        if (path is null)
            return; // No engine available in this environment; skip.

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var engine = UciChessEngine.LaunchProcess(path);

        var id = await engine.HandshakeAsync(cts.Token);
        Assert.False(string.IsNullOrWhiteSpace(id.Name));
        Assert.True(id.SupportsElo);

        await engine.NewGameAsync(cts.Token);
        var result = await engine.AnalyzeAsync("startpos", SearchLimits.ToDepth(12), cts.Token);

        Assert.InRange(result.BestMove.Length, 4, 5); // UCI long algebraic, e.g. e2e4 or e7e8q
        Assert.NotNull(result.Evaluation);
    }

    [Fact]
    public async Task Real_engine_returns_requested_number_of_lines()
    {
        var path = EnginePath;
        if (path is null)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var engine = UciChessEngine.LaunchProcess(path);

        await engine.HandshakeAsync(cts.Token);
        await engine.NewGameAsync(cts.Token);
        var result = await engine.AnalyzeAsync("startpos", SearchLimits.ToDepth(12, multiPv: 3), cts.Token);

        Assert.Equal(3, result.Lines.Count);
    }
}
