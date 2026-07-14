namespace Kaissa.Training.Play;

/// <summary>
/// A rough single-game "you played like ~X" estimate from the game's accuracy, nudged toward the
/// opponent's strength. Not a rating change (that is handled elsewhere) - just an at-a-glance read on
/// how strong the play in this one game was. Deliberately simple and clearly an estimate.
/// </summary>
public static class PerformanceRating
{
    // Accuracy -> rating anchors (monotonic), interpolated linearly between the points.
    private static readonly (double Acc, double Elo)[] Anchors =
    {
        (40, 400), (55, 800), (65, 1100), (75, 1500), (82, 1800), (88, 2100), (93, 2400), (97, 2700), (100, 2900),
    };

    public static int Estimate(double accuracy, int opponentElo)
    {
        double baseElo = FromAccuracy(Math.Clamp(accuracy, 0, 100));
        double opp = Math.Clamp(opponentElo, 100, 3000);
        double blended = 0.8 * baseElo + 0.2 * opp;
        return (int)Math.Round(Math.Clamp(blended, 100, 3000));
    }

    private static double FromAccuracy(double acc)
    {
        if (acc <= Anchors[0].Acc) return Anchors[0].Elo;
        for (int i = 1; i < Anchors.Length; i++)
        {
            if (acc <= Anchors[i].Acc)
            {
                var (a0, e0) = Anchors[i - 1];
                var (a1, e1) = Anchors[i];
                double t = (acc - a0) / (a1 - a0);
                return e0 + t * (e1 - e0);
            }
        }
        return Anchors[^1].Elo;
    }
}
