namespace Kaissa.Training.Play;

/// <summary>
/// Aggregated play signals collected from the player's own finished games, fed to
/// <see cref="WeaknessDashboard"/>. All fields are plain accumulators so the client can persist them and
/// the scoring stays pure and testable. Counts that have no games behind them make their axis show as
/// "not enough games yet" rather than a misleading zero.
/// </summary>
public sealed class WeaknessInput
{
    public double Rating { get; init; } = 800;

    // Opening / Endgame: averaged per-game phase accuracy (0-100), null when never measured.
    public double? OpeningAccuracy { get; init; }
    public double? EndgameAccuracy { get; init; }

    // Tactics: how many were taken vs left on the board, across fork/pin/mate/hanging.
    public int TacticsFound { get; init; }
    public int TacticsMissed { get; init; }

    // Advantage capitalization: of games where a clear winning edge was reached, how many were won.
    public int AdvantageGames { get; init; }
    public int AdvantageConverted { get; init; }

    // Resourcefulness: of games where a clearly losing position was reached, how many were not lost.
    public int LosingGames { get; init; }
    public int LosingSaved { get; init; }

    // Time management: over timed games, the averaged share of base time still on the clock at the end
    // (0-1); flagged games contribute 0. Only meaningful when TimedGames > 0.
    public int TimedGames { get; init; }
    public double TimeClockShare { get; init; }
}

/// <summary>
/// One dashboard axis: the player's score, the modeled peer baseline, a plain-language read, and the
/// scene the "train this" shortcut should route to (null when the axis has no direct drill).
/// </summary>
public sealed record AxisScore(string Name, int Score, int Peer, bool HasData, string Verdict, string Line, string? DrillRoute);

/// <summary>
/// The six-axis weakness report (Tactics, Endgame, Advantage Capitalization, Resourcefulness, Time
/// Management, Opening), computed from the player's own game history. Each axis is scored 0-100 and set
/// against a peer baseline that rises with rating - we have no crowd database, so the baseline is a
/// modeled "typical at your level", labelled as such in the UI, not real peer data. The verdict and the
/// one-line summary are templated (no engine, no network, no LLM), in keeping with the free/offline rule.
/// </summary>
public static class WeaknessDashboard
{
    // "Behind" / "even" / "ahead" is decided by this many points around the peer baseline.
    private const int Band = 6;

    // Below this many contributing games an axis is treated as not-yet-measurable.
    private const int MinGames = 1;

    public static IReadOnlyList<AxisScore> Compute(WeaknessInput input)
    {
        double r = input.Rating;

        int tacticsTotal = input.TacticsFound + input.TacticsMissed;
        var axes = new List<AxisScore>
        {
            Axis("Tactics",
                tacticsTotal >= MinGames,
                Pct(input.TacticsFound, tacticsTotal),
                PeerTactics(r),
                found => $"You take {found}% of the tactics on the board",
                "spot forks, pins and mates",
                "tactics"),

            Axis("Endgame",
                input.EndgameAccuracy.HasValue,
                (int)System.Math.Round(input.EndgameAccuracy ?? 0),
                PeerAccuracy(r) - 3,
                acc => $"Your endgame accuracy averages {acc}%",
                "drill won and drawn endings",
                "endgame"),

            Axis("Advantage capitalization",
                input.AdvantageGames >= MinGames,
                Pct(input.AdvantageConverted, input.AdvantageGames),
                PeerConversion(r),
                pct => $"You convert {pct}% of winning positions into wins",
                "practice closing out won games",
                "Play"),

            Axis("Resourcefulness",
                input.LosingGames >= MinGames,
                Pct(input.LosingSaved, input.LosingGames),
                PeerResourcefulness(r),
                pct => $"You save {pct}% of losing positions",
                "practice defence and counterplay",
                "Play"),

            Axis("Time management",
                input.TimedGames >= MinGames,
                (int)System.Math.Round(System.Math.Clamp(input.TimeClockShare, 0, 1) * 100),
                45,
                pct => $"You finish timed games with {pct}% of your clock left",
                "spend time earlier, not all at the end",
                "Play"),

            Axis("Opening",
                input.OpeningAccuracy.HasValue,
                (int)System.Math.Round(input.OpeningAccuracy ?? 0),
                PeerAccuracy(r),
                acc => $"Your opening accuracy averages {acc}%",
                "study your most-played openings",
                "opening"),
        };

        return axes;
    }

    /// <summary>The weakest axis that has data and sits behind its peer baseline, or null if none.</summary>
    public static AxisScore? WeakestActionable(IReadOnlyList<AxisScore> axes)
    {
        AxisScore? worst = null;
        foreach (var a in axes)
        {
            if (!a.HasData || a.DrillRoute == null || a.Score >= a.Peer) continue;
            if (worst == null || (a.Score - a.Peer) < (worst.Score - worst.Peer)) worst = a;
        }
        return worst;
    }

    private static AxisScore Axis(
        string name, bool hasData, int score, int peer,
        System.Func<int, string> lead, string drillHint, string? drillRoute)
    {
        peer = System.Math.Clamp(peer, 0, 100);
        score = System.Math.Clamp(score, 0, 100);
        if (!hasData)
            return new AxisScore(name, 0, peer, false, "none", "Not enough games yet.", drillRoute);

        string verdict = score >= peer + Band ? "ahead" : score <= peer - Band ? "behind" : "even";
        string tail = verdict switch
        {
            "ahead" => $" - ahead of the ~{peer}% typical at your level.",
            "behind" => $" - behind the ~{peer}% typical at your level; {drillHint}.",
            _ => $" - about the ~{peer}% typical at your level.",
        };
        return new AxisScore(name, score, peer, true, verdict, lead(score) + tail, drillRoute);
    }

    private static int Pct(int part, int whole) => whole <= 0 ? 0 : (int)System.Math.Round(100.0 * part / whole);

    // Modeled peer baselines. These rise with rating and are intentionally conservative; they represent a
    // "typical player at this rating", not measured crowd data. Tunable in one place.
    private static int PeerAccuracy(double rating) =>
        (int)System.Math.Round(System.Math.Clamp(60 + (rating - 800) / 45.0, 60, 92));

    private static int PeerTactics(double rating) =>
        (int)System.Math.Round(System.Math.Clamp(50 + (rating - 800) / 40.0, 50, 88));

    private static int PeerConversion(double rating) =>
        (int)System.Math.Round(System.Math.Clamp(55 + (rating - 800) / 40.0, 55, 90));

    private static int PeerResourcefulness(double rating) =>
        (int)System.Math.Round(System.Math.Clamp(25 + (rating - 800) / 60.0, 25, 55));
}
