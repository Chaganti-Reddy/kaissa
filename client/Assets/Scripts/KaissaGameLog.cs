using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// A small local log of finished-game accuracy scores, so the Stats screen can show how accurately the
// player has been playing over time. Kept in the same folder as the other player data.
public static class KaissaGameLog
{
    [Serializable]
    private sealed class Data
    {
        public List<double> accuracies = new();
        public List<int> results = new(); // parallel to accuracies: 0 loss, 1 draw, 2 win
        public List<int> quality = new(); // cumulative move-quality counts: best,excellent,good,inaccuracy,mistake,blunder
        public List<double> phaseOpen = new(), phaseMid = new(), phaseEnd = new(); // per-game phase accuracies
        public List<int> tacticsFound = new();   // cumulative tactics taken   [fork, pin, mate, hanging]
        public List<int> tacticsMissed = new();  // cumulative tactics missed  [fork, pin, mate, hanging]

        // Outcome signals for the weakness dashboard (see WeaknessDashboard in the core).
        public int advGames, advConverted;   // reached a winning edge; won it
        public int losingGames, losingSaved; // reached a losing position; did not lose
        public int timedGames;               // games played with a clock
        public double timeShareSum;          // sum of end-of-game clock share (0-1) over timed games
    }

    public static readonly string[] QualityNames = { "Best", "Excellent", "Good", "Inaccuracy", "Mistake", "Blunder" };

    private const int MaxEntries = 100;
    private static Data _data;
    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "kaissa-games.json");

    private static Data D
    {
        get
        {
            if (_data == null)
                _data = File.Exists(Path) ? JsonUtility.FromJson<Data>(File.ReadAllText(Path)) ?? new Data() : new Data();
            return _data;
        }
    }

    public static int Count => D.accuracies.Count;
    public static double Average => D.accuracies.Count == 0 ? 0 : D.accuracies.Average();

    // The most recent up-to-n game accuracies, oldest-first.
    public static IReadOnlyList<double> Recent(int n) =>
        D.accuracies.Skip(Math.Max(0, D.accuracies.Count - n)).ToList();

    public static int Wins => D.results.Count(r => r == 2);
    public static int Draws => D.results.Count(r => r == 1);
    public static int Losses => D.results.Count(r => r == 0);

    public static void Record(double accuracy, int result = 1)
    {
        D.accuracies.Add(accuracy);
        D.results.Add(result);
        if (D.accuracies.Count > MaxEntries) D.accuracies.RemoveRange(0, D.accuracies.Count - MaxEntries);
        if (D.results.Count > MaxEntries) D.results.RemoveRange(0, D.results.Count - MaxEntries);
        File.WriteAllText(Path, JsonUtility.ToJson(D));
    }

    // Cumulative move-quality mix + per-phase accuracy from a game review.
    public static void RecordReview(IEnumerable<string> moveQualities, double? open, double? mid, double? end)
    {
        while (D.quality.Count < 6) D.quality.Add(0);
        foreach (var q in moveQualities)
        {
            int i = Array.IndexOf(QualityNames, q);
            if (i >= 0) D.quality[i]++;
        }
        if (open is { } o) D.phaseOpen.Add(o);
        if (mid is { } m) D.phaseMid.Add(m);
        if (end is { } e) D.phaseEnd.Add(e);
        Cap(D.phaseOpen); Cap(D.phaseMid); Cap(D.phaseEnd);
        File.WriteAllText(Path, JsonUtility.ToJson(D));
    }

    private static void Cap(List<double> l) { if (l.Count > MaxEntries) l.RemoveRange(0, l.Count - MaxEntries); }

    // Accumulate found/missed tactics from a review, each indexed [fork, pin, mate, hanging].
    public static void RecordTactics(IReadOnlyList<int> found, IReadOnlyList<int> missed)
    {
        Pad(D.tacticsFound); Pad(D.tacticsMissed);
        for (int i = 0; i < 4; i++)
        {
            if (found != null && i < found.Count) D.tacticsFound[i] += found[i];
            if (missed != null && i < missed.Count) D.tacticsMissed[i] += missed[i];
        }
        File.WriteAllText(Path, JsonUtility.ToJson(D));
    }

    public static IReadOnlyList<int> TacticsFound { get { Pad(D.tacticsFound); return D.tacticsFound; } }
    public static IReadOnlyList<int> TacticsMissed { get { Pad(D.tacticsMissed); return D.tacticsMissed; } }
    public static readonly string[] TacticNames = { "Forks", "Pins", "Mates", "Hanging" };
    private static void Pad(List<int> l) { while (l.Count < 4) l.Add(0); }

    // Per-game outcome signals derived from the review eval curve, the result, and (for timed games) the
    // clock. evalSeries is player-perspective centipawns per ply; result is 0 loss / 1 draw / 2 win;
    // clockShare is the fraction of base time left at the end (only for timed games), null otherwise.
    public static void RecordOutcome(IReadOnlyList<int> evalSeries, int result, double? clockShare)
    {
        if (evalSeries != null && evalSeries.Count > 0)
        {
            int peak = int.MinValue, trough = int.MaxValue;
            foreach (int cp in evalSeries) { if (cp > peak) peak = cp; if (cp < trough) trough = cp; }
            if (peak >= 200) { D.advGames++; if (result == 2) D.advConverted++; }
            if (trough <= -200) { D.losingGames++; if (result != 0) D.losingSaved++; }
        }
        if (clockShare is { } share)
        {
            D.timedGames++;
            D.timeShareSum += Math.Clamp(share, 0, 1);
        }
        File.WriteAllText(Path, JsonUtility.ToJson(D));
    }

    public static int AdvantageGames => D.advGames;
    public static int AdvantageConverted => D.advConverted;
    public static int LosingGames => D.losingGames;
    public static int LosingSaved => D.losingSaved;
    public static int TimedGames => D.timedGames;
    public static double TimeClockShare => D.timedGames > 0 ? D.timeShareSum / D.timedGames : 0;

    public static IReadOnlyList<int> QualityMix => D.quality.Count >= 6 ? D.quality : new List<int> { 0, 0, 0, 0, 0, 0 };
    public static double? PhaseOpen => D.phaseOpen.Count > 0 ? D.phaseOpen.Average() : (double?)null;
    public static double? PhaseMid => D.phaseMid.Count > 0 ? D.phaseMid.Average() : (double?)null;
    public static double? PhaseEnd => D.phaseEnd.Count > 0 ? D.phaseEnd.Average() : (double?)null;
}
