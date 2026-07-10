using System.Globalization;
using System.Text;

namespace Kaissa.Chess.Engine;

/// <summary>
/// Speaks the UCI protocol over an <see cref="IUciTransport"/>. This class contains no process
/// or platform code, so it is fully unit-testable against a scripted transport.
/// </summary>
public sealed class UciChessEngine : IChessEngine
{
    private const int DefaultDepth = 12;

    private readonly IUciTransport _transport;
    private bool _started;

    public UciChessEngine(IUciTransport transport) => _transport = transport;

    /// <summary>Convenience factory for running a UCI engine executable out-of-process.</summary>
    public static UciChessEngine LaunchProcess(string executablePath) =>
        new(new ProcessUciTransport(executablePath));

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        await _transport.StartAsync(cancellationToken).ConfigureAwait(false);
        _started = true;
    }

    public async Task<EngineIdentification> HandshakeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);

        string name = "", author = "";
        var options = new List<UciOption>();

        await _transport.SendLineAsync("uci", cancellationToken).ConfigureAwait(false);
        await ReadUntilAsync(line =>
        {
            if (line.StartsWith("id name ", StringComparison.Ordinal))
                name = line["id name ".Length..].Trim();
            else if (line.StartsWith("id author ", StringComparison.Ordinal))
                author = line["id author ".Length..].Trim();
            else if (line.StartsWith("option ", StringComparison.Ordinal))
                options.Add(UciOption.Parse(line));

            return line.AsSpan().Trim().SequenceEqual("uciok");
        }, cancellationToken).ConfigureAwait(false);

        return new EngineIdentification(name, author, options);
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        await _transport.SendLineAsync("isready", cancellationToken).ConfigureAwait(false);
        await ReadUntilAsync(line => line.AsSpan().Trim().SequenceEqual("readyok"), cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public async Task SetOptionAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        await _transport.SendLineAsync($"setoption name {name} value {value}", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ConfigureStrengthAsync(StrengthSettings strength, CancellationToken cancellationToken = default)
    {
        if (strength.Elo is int elo)
        {
            await SetOptionAsync("UCI_LimitStrength", "true", cancellationToken).ConfigureAwait(false);
            await SetOptionAsync("UCI_Elo", elo.ToString(CultureInfo.InvariantCulture), cancellationToken)
                .ConfigureAwait(false);
        }
        else if (strength.SkillLevel is int level)
        {
            await SetOptionAsync("UCI_LimitStrength", "false", cancellationToken).ConfigureAwait(false);
            await SetOptionAsync("Skill Level", level.ToString(CultureInfo.InvariantCulture), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await SetOptionAsync("UCI_LimitStrength", "false", cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task NewGameAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        await _transport.SendLineAsync("ucinewgame", cancellationToken).ConfigureAwait(false);
        await IsReadyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SearchResult> AnalyzeAsync(
        string position, SearchLimits limits, CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);

        // Always set MultiPV (default 1) so a prior multi-line search - e.g. the analysis board asking
        // for 3 lines - never leaks into a later single-line search (eval bar, bot move) on a shared
        // engine, which would waste time computing extra lines.
        await SetOptionAsync("MultiPV", limits.MultiPv.ToString(CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);

        var positionCommand = position is "startpos" or ""
            ? "position startpos"
            : $"position fen {position}";
        await _transport.SendLineAsync(positionCommand, cancellationToken).ConfigureAwait(false);
        await _transport.SendLineAsync(BuildGoCommand(limits), cancellationToken).ConfigureAwait(false);

        var lines = new Dictionary<int, PvLine>();
        string bestMove = "";
        string? ponder = null;

        try
        {
            await ReadUntilAsync(line =>
            {
                if (line.StartsWith("info ", StringComparison.Ordinal) &&
                    line.Contains(" pv ", StringComparison.Ordinal))
                {
                    if (TryParseInfo(line) is { } pv)
                        lines[pv.MultiPvIndex] = pv;
                    return false;
                }

                if (line.StartsWith("bestmove ", StringComparison.Ordinal))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    bestMove = parts.Length > 1 ? parts[1] : "";
                    var ponderIndex = Array.IndexOf(parts, "ponder");
                    if (ponderIndex >= 0 && ponderIndex + 1 < parts.Length)
                        ponder = parts[ponderIndex + 1];
                    return true;
                }

                return false;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A cancelled search is still running in the engine and its "bestmove" is still coming. If we
            // just bail, that stale line would be read as the NEXT search's result and desync the
            // protocol. Tell the engine to stop and drain everything up to the ready ack, so the process
            // is clean for the next command (matters for the eval bar, which cancels on every move).
            await AbortSearchAsync().ConfigureAwait(false);
            throw;
        }

        var ordered = lines.Values.OrderBy(l => l.MultiPvIndex).ToList();
        return new SearchResult(bestMove, ponder, ordered);
    }

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _transport.SendLineAsync("stop", cancellationToken).AsTask();

    // Aborts an in-flight search and drains the engine's output back to a known-idle state.
    private async Task AbortSearchAsync()
    {
        try
        {
            await _transport.SendLineAsync("stop", CancellationToken.None).ConfigureAwait(false);
            await _transport.SendLineAsync("isready", CancellationToken.None).ConfigureAwait(false);
            await ReadUntilAsync(line => line.AsSpan().Trim().SequenceEqual("readyok"), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
            // Engine may have exited; the resync is best-effort.
        }
    }

    public ValueTask DisposeAsync() => _transport.DisposeAsync();

    private static string BuildGoCommand(SearchLimits limits)
    {
        var sb = new StringBuilder("go");
        if (limits.Depth is int depth)
            sb.Append(" depth ").Append(depth.ToString(CultureInfo.InvariantCulture));
        if (limits.MoveTime is TimeSpan moveTime)
            sb.Append(" movetime ").Append(((long)moveTime.TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
        if (limits.Nodes is long nodes)
            sb.Append(" nodes ").Append(nodes.ToString(CultureInfo.InvariantCulture));

        if (sb.Length == "go".Length)
            sb.Append(" depth ").Append(DefaultDepth);

        return sb.ToString();
    }

    internal static PvLine? TryParseInfo(string line)
    {
        var t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int depth = 0, multiPv = 1;
        Score? score = null;
        List<string>? moves = null;

        for (int i = 1; i < t.Length; i++)
        {
            switch (t[i])
            {
                case "depth" when i + 1 < t.Length && int.TryParse(t[i + 1], out var d):
                    depth = d;
                    i++;
                    break;
                case "multipv" when i + 1 < t.Length && int.TryParse(t[i + 1], out var m):
                    multiPv = m;
                    i++;
                    break;
                case "score" when i + 2 < t.Length && int.TryParse(t[i + 2], out var v):
                    score = t[i + 1] == "mate" ? Score.Mate(v) : Score.Centipawns(v);
                    i += 2;
                    break;
                case "pv":
                    moves = t[(i + 1)..].ToList();
                    i = t.Length;
                    break;
            }
        }

        if (score is null || moves is null || moves.Count == 0)
            return null;

        return new PvLine(multiPv, score.Value, depth, moves);
    }

    private async Task ReadUntilAsync(Func<string, bool> handle, CancellationToken cancellationToken)
    {
        var reader = _transport.Output;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var line))
            {
                if (handle(line))
                    return;
            }
        }

        throw new EngineProtocolException("Engine output ended before the expected response.");
    }

    private Task EnsureStartedAsync(CancellationToken cancellationToken) =>
        _started ? Task.CompletedTask : StartAsync(cancellationToken);
}

/// <summary>Raised when the engine's UCI output cannot be interpreted as expected.</summary>
public sealed class EngineProtocolException : Exception
{
    public EngineProtocolException(string message) : base(message) { }
}
