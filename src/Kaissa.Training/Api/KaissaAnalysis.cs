using Kaissa.Chess.Engine;
using Kaissa.Training.Play;

namespace Kaissa.Training.Api;

/// <summary>The engine's read on a position: evaluation (display + centipawns, side-to-move
/// perspective), best move, and the principal line.</summary>
public sealed record AnalysisLine(string Score, int Centipawns, string BestMove, IReadOnlyList<string> Moves);

/// <summary>
/// An analysis facade: evaluate any position or line with the engine at full strength. Powers an
/// analysis board and per-move game insights. Owns the engine process; dispose to stop it.
/// </summary>
public sealed class KaissaAnalysis : IAsyncDisposable
{
    private readonly IChessEngine _engine;
    private readonly bool _ownsEngine;

    private KaissaAnalysis(IChessEngine engine, bool ownsEngine)
    {
        _engine = engine;
        _ownsEngine = ownsEngine;
    }

    public static async Task<KaissaAnalysis> StartAsync(string enginePath, CancellationToken cancellationToken = default)
    {
        var engine = UciChessEngine.LaunchProcess(enginePath);
        await engine.HandshakeAsync(cancellationToken).ConfigureAwait(false);
        await engine.NewGameAsync(cancellationToken).ConfigureAwait(false);
        await engine.ConfigureStrengthAsync(StrengthSettings.Full, cancellationToken).ConfigureAwait(false);
        return new KaissaAnalysis(engine, ownsEngine: true);
    }

    /// <summary>
    /// Analyzes on an already-running engine (e.g. a shared, app-wide analysis process). Not owned:
    /// disposing leaves the process running. Full strength is set before each search so a shared engine
    /// always analyzes at full strength even if some other consumer capped it.
    /// </summary>
    public static KaissaAnalysis Attach(IChessEngine engine) => new(engine, ownsEngine: false);

    /// <summary>Evaluates a position (FEN or "startpos") to the given depth.</summary>
    public async Task<AnalysisLine> EvaluateAsync(string position, int depth = 16, CancellationToken cancellationToken = default)
    {
        // Full strength every time: a shared engine may have been capped by a bot game between calls.
        await _engine.ConfigureStrengthAsync(StrengthSettings.Full, cancellationToken).ConfigureAwait(false);
        var result = await _engine.AnalyzeAsync(position, SearchLimits.ToDepth(depth), cancellationToken)
            .ConfigureAwait(false);

        var moves = result.Lines.Count > 0 ? result.Lines[0].Moves : Array.Empty<string>();
        int cp = result.Evaluation is { } e ? MoveClassifier.ToCentipawns(e) : 0;
        return new AnalysisLine(result.Evaluation?.ToString() ?? "", cp, result.BestMove, moves);
    }

    /// <summary>
    /// Evaluates the top <paramref name="count"/> candidate lines (MultiPV) for a position at the given
    /// depth, ordered best first. Each line carries its own evaluation and principal continuation. Used
    /// by the analysis board to show several engine lines at once.
    /// </summary>
    public async Task<IReadOnlyList<AnalysisLine>> EvaluateLinesAsync(
        string position, int depth = 18, int count = 3, CancellationToken cancellationToken = default)
    {
        await _engine.ConfigureStrengthAsync(StrengthSettings.Full, cancellationToken).ConfigureAwait(false);
        var result = await _engine.AnalyzeAsync(position, SearchLimits.ToDepth(depth, Math.Max(1, count)), cancellationToken)
            .ConfigureAwait(false);

        var lines = new List<AnalysisLine>();
        foreach (var pv in result.Lines) // already ordered by MultiPV index (best first)
            lines.Add(new AnalysisLine(pv.Score.ToString(), MoveClassifier.ToCentipawns(pv.Score),
                pv.Moves.Count > 0 ? pv.Moves[0] : "", pv.Moves));
        return lines;
    }

    public ValueTask DisposeAsync() => _ownsEngine ? _engine.DisposeAsync() : default;
}
