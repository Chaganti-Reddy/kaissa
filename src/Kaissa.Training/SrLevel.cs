namespace Kaissa.Training;

/// <summary>
/// Maps an FSRS review interval onto a Chessable-style recall ladder: a level from 0 (new) up to 8
/// (long-term, effectively learned), with the classic 4h / 1d / 3d / 1wk / 2wk / 1mo / 3mo / 6mo rungs.
/// This is purely a presentation aid over our FSRS state - the schedule itself stays FSRS, this just
/// gives the drill a level number and a friendly "next in ..." label so recall progress is legible.
/// </summary>
public static class SrLevel
{
    public const int MaxLevel = 8;

    // Rung thresholds in days: reaching a rung raises the level. Interval >= 1d is L2, >= 6mo is L8.
    private static readonly int[] Rungs = { 1, 3, 7, 14, 30, 90, 180 };

    /// <summary>The recall level for an item: 0 if never seen, otherwise 1-8 by how far its interval reaches.</summary>
    public static int Level(int? intervalDays, bool seen)
    {
        if (!seen) return 0;
        int d = intervalDays ?? 0;
        int level = 1; // seen but shorter than a day
        for (int i = 0; i < Rungs.Length; i++)
            if (d >= Rungs[i]) level = i + 2;
        return System.Math.Min(level, MaxLevel);
    }

    /// <summary>An item at the top of the ladder is treated as learned.</summary>
    public static bool IsMastered(int level) => level >= MaxLevel;

    /// <summary>A short human label for an interval in days: 4h / 2d / 3wk / 5mo / 1y.</summary>
    public static string Friendly(int days)
    {
        if (days <= 0) return "today";
        if (days >= 365) { int y = days / 365; return $"{y}y"; }
        if (days >= 30) { int mo = days / 30; return $"{mo}mo"; }
        if (days >= 7) { int wk = days / 7; return $"{wk}wk"; }
        return $"{days}d";
    }
}
