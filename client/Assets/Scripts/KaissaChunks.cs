using System.Collections.Generic;
using System.Linq;
using Kaissa.Training;

// Persists chunk-recognition tallies (from ChunkScheduler) across sessions so the Stats page can show
// which structural chunks the player reads well and which they miss. Backed by a single settings string
// - "name;seen;correct" entries joined by '|'. Cosmetic/analytics only; never gates training.
public static class KaissaChunks
{
    public static void Record(string fen, bool solved)
    {
        if (string.IsNullOrEmpty(fen)) return;
        var sch = new ChunkScheduler(Load());
        sch.Record(fen, solved);
        KaissaSettings.ChunkStats = Serialize(sch.Snapshot());
    }

    public static IReadOnlyList<ChunkStat> Weakest(int n = 3, int minSeen = 2) =>
        new ChunkScheduler(Load()).Weakest(n, minSeen);

    public static IReadOnlyList<ChunkStat> Snapshot() => new ChunkScheduler(Load()).Snapshot();

    private static IEnumerable<ChunkStat> Load()
    {
        var raw = KaissaSettings.ChunkStats;
        if (string.IsNullOrEmpty(raw)) yield break;
        foreach (var entry in raw.Split('|'))
        {
            var parts = entry.Split(';');
            if (parts.Length == 3 && int.TryParse(parts[1], out int seen) && int.TryParse(parts[2], out int correct))
                yield return new ChunkStat(parts[0], seen, correct);
        }
    }

    private static string Serialize(IEnumerable<ChunkStat> stats) =>
        string.Join("|", stats.Select(s => $"{s.Chunk};{s.Seen};{s.Correct}"));
}
