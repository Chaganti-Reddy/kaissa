using System.Threading.Channels;
using Kaissa.Chess.Engine;

namespace Kaissa.Chess.Engine.Tests;

/// <summary>
/// An in-memory UCI engine that reacts to commands with canned, deterministic responses.
/// Lets the protocol logic in <see cref="UciChessEngine"/> be tested with no external process.
/// </summary>
internal sealed class ScriptedUciTransport : IUciTransport
{
    private static readonly string[][] Candidates =
    {
        new[] { "e2e4", "e7e5" },
        new[] { "d2d4", "d7d5" },
        new[] { "g1f3", "g8f6" },
    };

    private readonly Channel<string> _output = Channel.CreateUnbounded<string>();
    private int _multiPv = 1;

    public ChannelReader<string> Output => _output.Reader;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (parts.Length == 0 ? "" : parts[0])
        {
            case "uci":
                Emit("id name Kaissa.Scripted 1.0");
                Emit("id author Kaissa");
                Emit("option name UCI_LimitStrength type check default false");
                Emit("option name UCI_Elo type spin default 1320 min 1320 max 3190");
                Emit("option name MultiPV type spin default 1 min 1 max 5");
                Emit("uciok");
                break;

            case "isready":
                Emit("readyok");
                break;

            case "setoption":
                var valueIdx = Array.IndexOf(parts, "value");
                if (line.Contains("name MultiPV", StringComparison.Ordinal) &&
                    valueIdx >= 0 && valueIdx + 1 < parts.Length &&
                    int.TryParse(parts[valueIdx + 1], out var mpv))
                {
                    _multiPv = Math.Clamp(mpv, 1, Candidates.Length);
                }
                break;

            case "go":
                for (int i = 0; i < _multiPv && i < Candidates.Length; i++)
                    Emit($"info depth 12 multipv {i + 1} score cp {34 - i * 12} pv {string.Join(' ', Candidates[i])}");
                Emit("bestmove e2e4 ponder e7e5");
                break;
        }

        return ValueTask.CompletedTask;
    }

    private void Emit(string text) => _output.Writer.TryWrite(text);

    public ValueTask DisposeAsync()
    {
        _output.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
