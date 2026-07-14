using Kaissa.Chess.Engine;

namespace Kaissa.Training.Play;

/// <summary>
/// Quality of a played move. The scale mirrors the one post-game analysis tools use: from Brilliant
/// down to Blunder, with Book for opening theory and Miss for a squandered winning chance.
/// </summary>
public enum MoveQuality
{
    Brilliant,
    Great,
    Best,
    Book,
    Excellent,
    Good,
    Inaccuracy,
    Miss,
    Mistake,
    Blunder,
}

/// <summary>The inputs needed to classify a move beyond raw centipawn loss.</summary>
public readonly record struct MoveJudgement(
    int CentipawnLoss,   // eval given up versus the best move (player perspective, cp)
    int BestEvalCp,      // eval of the best move (player perspective)
    int PlayedEvalCp,    // eval after the played move (player perspective)
    int SecondBestGap,   // how much worse the second-best move is than the best (>= 0)
    bool IsBook,         // the position was still in opening theory and this was a book move
    bool IsSacrifice);   // the move gives up material yet keeps a clear advantage

/// <summary>
/// Classifies a move. The simple <see cref="Classify(int)"/> maps centipawn loss to the core
/// best/excellent/good/inaccuracy/mistake/blunder tiers; the richer <see cref="Classify(MoveJudgement)"/>
/// adds Book, Brilliant (a sound sacrifice), Great (the only good move), and Miss (a squandered chance).
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
        <= 10 => MoveQuality.Best,
        <= 25 => MoveQuality.Excellent,
        <= 50 => MoveQuality.Good,
        <= 100 => MoveQuality.Inaccuracy,
        <= 200 => MoveQuality.Mistake,
        _ => MoveQuality.Blunder,
    };

    public static MoveQuality Classify(MoveJudgement j)
    {
        if (j.IsBook)
            return MoveQuality.Book;

        int loss = j.CentipawnLoss;
        bool isBest = loss <= 10;

        // Brilliant (chess.com's definition): a best move that SACRIFICES material, leaves you NOT
        // losing (>= ~0), and where you were NOT already winning without it - i.e. the next-best
        // alternative was not itself clearly winning (best eval minus the gap to second-best stays
        // below a winning margin). A sac that only converts an already-won position is not brilliant.
        if (isBest && j.IsSacrifice && j.PlayedEvalCp >= -50 && (j.BestEvalCp - j.SecondBestGap) < 300)
            return MoveQuality.Brilliant;

        // A best move that is the only good one - every alternative is much worse - is Great.
        if (isBest && j.SecondBestGap >= 150)
            return MoveQuality.Great;

        if (loss <= 10) return MoveQuality.Best;
        if (loss <= 25) return MoveQuality.Excellent;
        if (loss <= 50) return MoveQuality.Good;

        // Miss: a clearly winning chance existed (best move was winning) and the move let it slip,
        // without outright losing. Distinct from a Mistake/Blunder that worsens an equal position.
        if (j.BestEvalCp >= 200 && loss >= 100 && j.PlayedEvalCp >= -100)
            return MoveQuality.Miss;

        if (loss <= 100) return MoveQuality.Inaccuracy;
        if (loss <= 200) return MoveQuality.Mistake;
        return MoveQuality.Blunder;
    }

    /// <summary>Whether a quality counts as an error worth practising (Miss, Mistake, or Blunder).</summary>
    public static bool IsMistake(MoveQuality q) =>
        q is MoveQuality.Miss or MoveQuality.Mistake or MoveQuality.Blunder;
}
