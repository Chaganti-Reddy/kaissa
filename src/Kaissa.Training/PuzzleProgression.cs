using Kaissa.Training.Api;

namespace Kaissa.Training;

/// <summary>
/// The hybrid progression layer that sits on top of the skill rating. Rating measures strength;
/// this measures engagement and mastery. Every solve earns XP (scaled by difficulty,
/// solving above your level, solving unaided, first-try, speed, and an in-session solve streak),
/// XP rolls up into named tiers, and each trained pattern gets its own mastery level derived from
/// how well the spaced-repetition schedule says the chunk has consolidated. Pure logic, no Unity,
/// so it is unit-testable and portable.
/// </summary>
public static class PuzzleProgression
{
    /// <summary>XP awarded for a solved puzzle. Zero for a failed one (call only on a solve).</summary>
    public static int XpForSolve(
        int puzzleRating,
        double playerRating,
        bool hintUsed,
        bool firstTry,
        double solveSeconds,
        int sessionSolveStreak)
    {
        double baseXp = Math.Clamp(puzzleRating / 10.0, 30, 300);

        // Solving above your level is worth more; below, a little less (never below half).
        double delta = puzzleRating - playerRating;
        double levelMult = Math.Clamp(1.0 + delta / 400.0, 0.5, 2.0);

        double mult = levelMult;
        if (hintUsed) mult *= 0.3;            // assisted: mostly progression, little reward
        else if (firstTry) mult *= 1.25;      // clean, unaided solve
        if (!hintUsed && solveSeconds > 0 && solveSeconds < 10) mult *= 1.15; // quick

        // In-session streak: +5% per consecutive solve, capped at +50%.
        mult *= 1.0 + Math.Min(0.5, 0.05 * Math.Max(0, sessionSolveStreak));

        return Math.Max(1, (int)Math.Round(baseXp * mult));
    }

    /// <summary>Named tier ladder, most-advanced last. More granular than a single league.</summary>
    public static readonly IReadOnlyList<(string Name, long MinXp)> Tiers = new (string, long)[]
    {
        ("Wood", 0),
        ("Stone", 500),
        ("Bronze", 1_500),
        ("Silver", 4_000),
        ("Gold", 9_000),
        ("Sapphire", 18_000),
        ("Ruby", 34_000),
        ("Diamond", 60_000),
        ("Master", 100_000),
        ("Grandmaster", 175_000),
    };

    public readonly record struct TierStanding(
        int Index, string Name, long TierMinXp, long? NextTierXp, long TotalXp)
    {
        public bool IsMax => NextTierXp is null;

        /// <summary>Progress through the current tier, 0..1 (1 at the max tier).</summary>
        public float Fraction
        {
            get
            {
                if (NextTierXp is not { } next) return 1f;
                long span = next - TierMinXp;
                return span <= 0 ? 1f : Math.Clamp((TotalXp - TierMinXp) / (float)span, 0f, 1f);
            }
        }

        public long XpIntoTier => TotalXp - TierMinXp;
        public long XpForNext => NextTierXp is { } n ? n - TierMinXp : 0;
    }

    public static TierStanding Standing(long totalXp)
    {
        int i = 0;
        for (int t = 0; t < Tiers.Count; t++)
            if (totalXp >= Tiers[t].MinXp) i = t;

        long? next = i + 1 < Tiers.Count ? Tiers[i + 1].MinXp : null;
        return new TierStanding(i, Tiers[i].Name, Tiers[i].MinXp, next, totalXp);
    }

    /// <summary>How consolidated a pattern's chunk is, on a 0..5 ladder.</summary>
    public enum Mastery { Unseen = 0, Learning = 1, Familiar = 2, Proficient = 3, Strong = 4, Mastered = 5 }

    public static readonly IReadOnlyList<string> MasteryLabels = new[]
    {
        "Unseen", "Learning", "Familiar", "Proficient", "Strong", "Mastered",
    };

    /// <summary>
    /// Maps a pattern's spaced-repetition state to a mastery level. Stability (days the memory is
    /// expected to hold) is the primary signal - it is exactly how well the chunk has stuck -
    /// tempered by lapses. A pattern with many lapses is capped below "Mastered".
    /// </summary>
    public static Mastery MasteryFor(ProgressRow row)
    {
        if (!row.Seen || row.Reps == 0) return Mastery.Unseen;

        Mastery byStability =
            row.StabilityDays >= 120 ? Mastery.Mastered
            : row.StabilityDays >= 45 ? Mastery.Strong
            : row.StabilityDays >= 14 ? Mastery.Proficient
            : row.StabilityDays >= 3 ? Mastery.Familiar
            : Mastery.Learning;

        // A shaky chunk (lapses rival reps) can't be called Mastered/Strong.
        if (row.Lapses > 0 && row.Lapses * 2 >= row.Reps && byStability > Mastery.Proficient)
            byStability = Mastery.Proficient;

        return byStability;
    }

    public static string MasteryLabel(Mastery m) => MasteryLabels[(int)m];
}
