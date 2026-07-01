using Kaissa.Chess.Engine;

namespace Kaissa.Training.Play;

/// <summary>Quality of a played move, judged by how much evaluation it lost versus the best move.</summary>
public enum MoveQuality
{
    Best,
    Good,
    Inaccuracy,
    Mistake,
    Blunder,
}

/// <summary>
/// Classifies a move from the centipawn loss against the engine's best move. Thresholds follow the
/// familiar best/good/inaccuracy/mistake/blunder scale used by post-game analysis tools.
/// </summary>
public static class MoveClassifier
{
    // A mate is represented as a large centipawn value that shrinks slightly with distance, so a
    // faster mate scores higher and losing a mate registers as a large swing.
    private const int MateBase = 100_000;

    public static int ToCentipawns(Score score) => score.Kind == ScoreKind.Mate
        ? (score.Value >= 0 ? MateBase - score.Value * 100 : -MateBase - score.Value * 100)
        : score.Value;

    public static MoveQuality Classify(int centipawnLoss) => centipawnLoss switch
    {
        <= 20 => MoveQuality.Best,
        <= 50 => MoveQuality.Good,
        <= 100 => MoveQuality.Inaccuracy,
        <= 200 => MoveQuality.Mistake,
        _ => MoveQuality.Blunder,
    };
}
