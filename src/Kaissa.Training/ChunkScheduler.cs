using System.Collections.Generic;
using System.Linq;

namespace Kaissa.Training;

/// <summary>How the player is doing on one chunk: how often it has come up and how often they solved it.</summary>
public sealed record ChunkStat(string Chunk, int Seen, int Correct)
{
    /// <summary>Solve rate on this chunk, 0..1 (0 when never seen).</summary>
    public double Accuracy => Seen == 0 ? 0 : (double)Correct / Seen;
}

/// <summary>
/// Tracks recognition of structural chunks (from <see cref="ChunkTagger"/>) across solved and missed
/// positions, so training can be organised around the chunks the player is weakest at - the app's core
/// premise (Chase-Simon chunk recognition). Every puzzle the player attempts is tagged by the chunks on
/// the side they were playing; a solve credits each of those chunks, a miss does not. Pure and
/// serialisable via <see cref="Snapshot"/>.
/// </summary>
public sealed class ChunkScheduler
{
    private readonly Dictionary<string, (int seen, int correct)> _stats = new();

    public ChunkScheduler(IEnumerable<ChunkStat>? seed = null)
    {
        if (seed == null) return;
        foreach (var s in seed) _stats[s.Chunk] = (s.Seen, s.Correct);
    }

    /// <summary>Record an attempt at a position: tag the side-to-move's chunks and credit them on a solve.</summary>
    public void Record(string fen, bool solved)
    {
        bool whiteToMove = SideToMove(fen);
        foreach (var chunk in ChunkTagger.Tag(fen).Where(c => c.White == whiteToMove).Select(c => c.Name).Distinct())
        {
            var (seen, correct) = _stats.TryGetValue(chunk, out var v) ? v : (0, 0);
            _stats[chunk] = (seen + 1, correct + (solved ? 1 : 0));
        }
    }

    /// <summary>All chunk stats, most-seen first.</summary>
    public IReadOnlyList<ChunkStat> Snapshot() =>
        _stats.Select(kv => new ChunkStat(kv.Key, kv.Value.seen, kv.Value.correct))
              .OrderByDescending(s => s.Seen)
              .ThenBy(s => s.Chunk, System.StringComparer.Ordinal)
              .ToList();

    /// <summary>
    /// The chunks to drill next: those seen at least <paramref name="minSeen"/> times, weakest solve
    /// rate first (ties broken by how often they came up), capped at <paramref name="n"/>.
    /// </summary>
    public IReadOnlyList<ChunkStat> Weakest(int n = 3, int minSeen = 2) =>
        Snapshot()
            .Where(s => s.Seen >= minSeen)
            .OrderBy(s => s.Accuracy)
            .ThenByDescending(s => s.Seen)
            .Take(n)
            .ToList();

    private static bool SideToMove(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length < 2 || parts[1] == "w";
    }
}
