using System.Collections.Generic;
using System.Linq;

namespace Kaissa.Training;

/// <summary>One move of a line under per-move spaced repetition: which line and ply, the move, its box.</summary>
public sealed record MoveTrainerItem(string LineId, int MoveIndex, string Uci, int Box, int DueDay);

/// <summary>
/// Per-move spaced repetition (Chessable's MoveTrainer model): every move in a line carries its OWN
/// review schedule, not the line as a whole, so the moves you keep forgetting resurface on their own
/// while the ones you know rest. Scheduling is delegated to <see cref="LeitnerScheduler"/>; this owns
/// the set of move-cards and serves the most-overdue one. Deterministic (days passed in) and testable.
/// </summary>
public sealed class MoveTrainer
{
    private readonly LeitnerScheduler _sched;
    private readonly Dictionary<(string line, int ply), (string uci, LeitnerCard card)> _cards = new();

    public MoveTrainer(LeitnerScheduler? scheduler = null) => _sched = scheduler ?? new LeitnerScheduler();

    /// <summary>Seed a line's moves as new cards (box 1, due immediately). Ignores moves already added.</summary>
    public void Add(string lineId, IReadOnlyList<string> uciMoves)
    {
        for (int i = 0; i < uciMoves.Count; i++)
        {
            var key = (lineId, i);
            if (!_cards.ContainsKey(key))
                _cards[key] = (uciMoves[i], new LeitnerCard($"{lineId}#{i}", 1, 0));
        }
    }

    public int Count => _cards.Count;

    /// <summary>Due moves on or before <paramref name="today"/>, hardest (lowest box) first.</summary>
    public IReadOnlyList<MoveTrainerItem> Due(int today) =>
        _cards.Where(kv => kv.Value.card.DueDay <= today)
              .OrderBy(kv => kv.Value.card.Box)
              .ThenBy(kv => kv.Key.line, System.StringComparer.Ordinal)
              .ThenBy(kv => kv.Key.ply)
              .Select(kv => new MoveTrainerItem(kv.Key.line, kv.Key.ply, kv.Value.uci, kv.Value.card.Box, kv.Value.card.DueDay))
              .ToList();

    /// <summary>The single most-due move to drill next, or null if none are due.</summary>
    public MoveTrainerItem? NextDue(int today) => Due(today).FirstOrDefault();

    /// <summary>Record a review outcome for one move of a line.</summary>
    public void Review(string lineId, int moveIndex, bool correct, int today)
    {
        var key = (lineId, moveIndex);
        if (!_cards.TryGetValue(key, out var entry)) return;
        _cards[key] = (entry.uci, _sched.Review(entry.card, correct, today));
    }

    /// <summary>Box of a specific move (0 if unknown), for progress display and saving.</summary>
    public int BoxOf(string lineId, int moveIndex) =>
        _cards.TryGetValue((lineId, moveIndex), out var e) ? e.card.Box : 0;

    /// <summary>All cards, for persistence.</summary>
    public IReadOnlyList<MoveTrainerItem> Cards() =>
        _cards.Select(kv => new MoveTrainerItem(kv.Key.line, kv.Key.ply, kv.Value.uci, kv.Value.card.Box, kv.Value.card.DueDay))
              .ToList();
}
