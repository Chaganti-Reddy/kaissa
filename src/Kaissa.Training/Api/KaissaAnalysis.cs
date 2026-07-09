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

    private KaissaAnalysis(IChessEngine engine) => _engine = engine;

    public static async Task<KaissaAnalysis> StartAsync(string enginePath, CancellationToken cancellationToken = default)
    {
        var engine = UciChessEngine.LaunchProcess(enginePath);
        await engine.HandshakeAsync(cancellationToken).ConfigureAwait(false);
        await engine.NewGameAsync(cancellationToken).ConfigureAwait(false);
        await engine.ConfigureStrengthAsync(StrengthSettings.Full, cancellationToken).ConfigureAwait(false);
        return new KaissaAnalysis(engine);
    }

    /// <summary>Evaluates a position (FEN or "startpos") to the given depth.</summary>
    public async Task<AnalysisLine> EvaluateAsync(string position, int depth = 16, CancellationToken cancellationToken = default)
    {
        var result = await _engine.AnalyzeAsync(position, SearchLimits.ToDepth(depth), cancellationToken)
            .ConfigureAwait(false);

        var moves = result.Lines.Count > 0 ? result.Lines[0].Moves : Array.Empty<string>();
        int cp = result.Evaluation is { } e ? MoveClassifier.ToCentipawns(e) : 0;
        return new AnalysisLine(result.Evaluation?.ToString() ?? "", cp, result.BestMove, moves);
    }

    public ValueTask DisposeAsync() => _engine.DisposeAsync();
}
