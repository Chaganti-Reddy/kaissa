using Kaissa.Chess.Engine;

namespace Kaissa.Training.Play;

/// <summary>
/// A human-like opponent backed by a Maia network running on lc0. Maia is a neural net trained on
/// millions of human games; evaluated at a single node (no tree search) it reproduces the move a human
/// of its rating band would most likely play - natural developing moves, human mistakes and all.
/// Strength is the chosen net (maia-1100 .. maia-1900), not an engine Elo cap.
/// </summary>
public sealed class MaiaOpponent : IOpponent
{
    private readonly IChessEngine _engine;
    private readonly string _weightsPath;
    private readonly int _elo;
    private bool _weightsSet;

    public MaiaOpponent(IChessEngine engine, string weightsPath, int displayElo)
    {
        _engine = engine;
        _weightsPath = weightsPath;
        _elo = displayElo;
    }

    public int TargetElo(double playerRating) => _elo;

    public async Task<string> ChooseMoveAsync(string fen, double playerRating, CancellationToken cancellationToken = default)
    {
        if (!_weightsSet)
        {
            // Loading the net is expensive, so do it once per opponent, not per move.
            await _engine.SetOptionAsync("WeightsFile", _weightsPath, cancellationToken).ConfigureAwait(false);
            _weightsSet = true;
        }

        // One node = a single network pass with no search: Maia's raw, human-like policy move.
        var result = await _engine.AnalyzeAsync(fen, new SearchLimits { Nodes = 1 }, cancellationToken).ConfigureAwait(false);
        return result.BestMove;
    }
}
