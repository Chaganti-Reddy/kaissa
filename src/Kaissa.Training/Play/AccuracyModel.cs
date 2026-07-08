namespace Kaissa.Training.Play;

/// <summary>
/// Turns per-move centipawn losses into a game accuracy percentage (0–100), the way chess sites
/// report it. The model is the widely-used Lichess formula: each evaluation is mapped to an expected
/// win percentage, a move's accuracy is a function of how much win percentage it gave up versus the
/// engine's best, and the game's accuracy is the mean of its moves. This keeps a single large blunder
/// from mattering less than several small ones, and rewards near-best play.
/// </summary>
public static class AccuracyModel
{
    /// <summary>Expected win percentage (0–100) for the side to move at a centipawn evaluation.</summary>
    public static double WinPercent(int centipawns)
    {
        // Logistic used by Lichess; clamp keeps mate-scale evals from overflowing exp().
        double cp = Math.Max(-2000, Math.Min(2000, centipawns));
        return 50.0 + 50.0 * (2.0 / (1.0 + Math.Exp(-0.00368208 * cp)) - 1.0);
    }

    /// <summary>Accuracy (0–100) for a single move that gave up <paramref name="winPercentDrop"/> win%.</summary>
    public static double MoveAccuracy(double winPercentDrop)
    {
        double drop = Math.Max(0.0, winPercentDrop);
        double acc = 103.1668 * Math.Exp(-0.04354 * drop) - 3.1669;
        return Math.Max(0.0, Math.Min(100.0, acc));
    }

    /// <summary>
    /// Game accuracy (0–100) for one side, from that side's move assessments. Returns 100 when the
    /// side made no assessed moves (nothing could have gone wrong).
    /// </summary>
    public static double GameAccuracy(IEnumerable<MoveAssessment> playerMoves)
    {
        double sum = 0.0;
        int count = 0;
        foreach (var m in playerMoves)
        {
            double winBest = WinPercent(m.BestEvalCp);
            double winPlayed = WinPercent(m.BestEvalCp - m.CentipawnLoss);
            sum += MoveAccuracy(winBest - winPlayed);
            count++;
        }

        return count == 0 ? 100.0 : sum / count;
    }
}
