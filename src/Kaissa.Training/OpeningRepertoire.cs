using System.Text.Json;
using Kaissa.Chess.Rules;
using Kaissa.Learning;

namespace Kaissa.Training;

/// <summary>One line in a repertoire: the moves (UCI) from the start, and which side the player takes.
/// Only the player's moves become drilled cards; the opponent's replies are played automatically.</summary>
public sealed record RepertoireLine(string Id, string Name, Side PlayerSide, IReadOnlyList<string> Moves);

/// <summary>A default starter repertoire (the same openings the book-walk trainer offers, with a side).</summary>
public static class OpeningRepertoire
{
    public static IReadOnlyList<RepertoireLine> Default { get; } = new[]
    {
        new RepertoireLine("italian", "Italian Game", Side.White, new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1c4" }),
        new RepertoireLine("ruy_lopez", "Ruy Lopez", Side.White, new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1b5" }),
        new RepertoireLine("sicilian", "Sicilian Defence", Side.Black, new[] { "e2e4", "c7c5", "g1f3", "d7d6", "d2d4", "g8f6" }),
        new RepertoireLine("queens_gambit", "Queen's Gambit", Side.White, new[] { "d2d4", "d7d5", "c2c4" }),
        new RepertoireLine("london", "London System", Side.White, new[] { "d2d4", "d7d5", "g1f3", "g8f6", "c1f4" }),
    };
}

/// <summary>A single thing to recall: the player is to move in this position and must find the book move.</summary>
public sealed record RepertoireCard(string Key, string LineName, string Fen, bool WhiteToMove, string ExpectedMove);

/// <summary>The graded outcome of recalling one repertoire move.</summary>
public sealed record RepertoireResult(bool Correct, string ExpectedMove, double IntervalDays);

/// <summary>Per-decision spaced-repetition state for a repertoire, keyed "lineId:ply". Serialisable.</summary>
public sealed class OpeningProgress
{
    private readonly Dictionary<string, DecisionState> _states = new();

    public bool Has(string key) => _states.ContainsKey(key);
    public int Count => _states.Count;

    public DecisionState GetOrCreate(string key)
    {
        if (!_states.TryGetValue(key, out var s))
            _states[key] = s = new DecisionState();
        return s;
    }

    public IEnumerable<(string Key, DateTime Due)> Due(DateTime now) =>
        _states.Where(kv => kv.Value.DueUtc is { } d && d <= now).Select(kv => (kv.Key, kv.Value.DueUtc!.Value));

    public string ToJson() => JsonSerializer.Serialize(
        _states.ToDictionary(kv => kv.Key, kv => new StateDto
        {
            Stability = kv.Value.State?.Stability,
            Difficulty = kv.Value.State?.Difficulty,
            LastReviewUtc = kv.Value.LastReviewUtc,
            DueUtc = kv.Value.DueUtc,
            Reps = kv.Value.Reps,
            Lapses = kv.Value.Lapses,
        }));

    public static OpeningProgress FromJson(string json)
    {
        var progress = new OpeningProgress();
        var dtos = JsonSerializer.Deserialize<Dictionary<string, StateDto>>(json) ?? new();
        foreach (var (key, dto) in dtos)
            progress._states[key] = new DecisionState
            {
                State = dto.Stability is { } s && dto.Difficulty is { } d ? new MemoryState(s, d) : null,
                LastReviewUtc = dto.LastReviewUtc,
                DueUtc = dto.DueUtc,
                Reps = dto.Reps,
                Lapses = dto.Lapses,
            };
        return progress;
    }

    public sealed class DecisionState
    {
        public MemoryState? State { get; set; }
        public DateTime? LastReviewUtc { get; set; }
        public DateTime? DueUtc { get; set; }
        public int Reps { get; set; }
        public int Lapses { get; set; }
        public bool Seen => State is not null;
    }

    private sealed class StateDto
    {
        public double? Stability { get; init; }
        public double? Difficulty { get; init; }
        public DateTime? LastReviewUtc { get; init; }
        public DateTime? DueUtc { get; init; }
        public int Reps { get; init; }
        public int Lapses { get; init; }
    }
}

/// <summary>
/// Drills a repertoire with spaced repetition: the player recalls their own moves; the opponent's are
/// filled in. Each of the player's decisions is scheduled independently (FSRS), reviewed when due, and
/// a wrong recall is treated as a lapse and resurfaced soon. Policy mirrors the puzzle trainer:
/// due first, then something new, then reinforce the weakest.
/// </summary>
public sealed class RepertoireSession
{
    private readonly List<Decision> _decisions = new();
    private readonly OpeningProgress _progress;
    private readonly FsrsScheduler _scheduler;
    private readonly IClock _clock;
    private Decision? _current;

    public RepertoireSession(IReadOnlyList<RepertoireLine> lines, OpeningProgress progress, IClock clock,
        FsrsScheduler? scheduler = null)
    {
        _progress = progress;
        _clock = clock;
        _scheduler = scheduler ?? new FsrsScheduler();
        BuildDecisions(lines);
    }

    public int Total => _decisions.Count;
    public int DueCount => _progress.Due(_clock.UtcNow).Count();

    private void BuildDecisions(IReadOnlyList<RepertoireLine> lines)
    {
        foreach (var line in lines)
        {
            var game = ChessGame.Start();
            for (int ply = 0; ply < line.Moves.Count; ply++)
            {
                var move = line.Moves[ply];
                if (game.SideToMove == line.PlayerSide)
                    _decisions.Add(new Decision($"{line.Id}:{ply}", line.Name, game.Fen,
                        game.SideToMove == Side.White, move));
                if (!game.TryMakeMove(move))
                    break; // a bad line stops here rather than throwing
            }
        }
    }

    /// <summary>The next decision to recall, or null if the repertoire is empty.</summary>
    public RepertoireCard? Next()
    {
        if (_decisions.Count == 0)
            return null;

        var now = _clock.UtcNow;

        // 1. Most overdue.
        var dueKey = _progress.Due(now).OrderBy(d => d.Due).Select(d => d.Key).FirstOrDefault();
        _current = dueKey is not null ? _decisions.First(d => d.Key == dueKey)
            // 2. Something not yet learned.
            : _decisions.FirstOrDefault(d => !IsSeen(d))
            // 3. Reinforce the weakest (least stable) seen decision.
            ?? _decisions.OrderBy(Stability).First();

        return new RepertoireCard(_current.Key, _current.LineName, _current.Fen, _current.WhiteToMove, _current.Expected);
    }

    /// <summary>Grades the recalled move for the current card and schedules its next review.</summary>
    public RepertoireResult Submit(string move, TimeSpan thinkingTime)
    {
        if (_current is null)
            throw new InvalidOperationException("Call Next() before Submit().");

        var decision = _current;
        var game = ChessGame.FromFen(decision.Fen);
        var uci = game.ResolveToUci(move);
        bool correct = uci is not null && string.Equals(uci, decision.Expected, StringComparison.OrdinalIgnoreCase);

        var rating = !correct ? Rating.Again
            : thinkingTime <= TimeSpan.FromSeconds(3) ? Rating.Easy
            : thinkingTime <= TimeSpan.FromSeconds(12) ? Rating.Good
            : Rating.Hard;

        var state = _progress.GetOrCreate(decision.Key);
        var now = _clock.UtcNow;
        double elapsedDays = state.LastReviewUtc is { } last ? (now - last).TotalDays : 0;
        var review = _scheduler.Review(state.State, elapsedDays, rating);

        state.State = review.State;
        state.LastReviewUtc = now;
        state.DueUtc = now.AddDays(review.IntervalDays);
        state.Reps++;
        if (rating == Rating.Again)
            state.Lapses++;

        _current = null;
        return new RepertoireResult(correct, decision.Expected, review.IntervalDays);
    }

    private bool IsSeen(Decision d) => _progress.Has(d.Key) && _progress.GetOrCreate(d.Key).Seen;
    private double Stability(Decision d) => _progress.Has(d.Key) ? _progress.GetOrCreate(d.Key).State?.Stability ?? 0 : 0;

    private sealed record Decision(string Key, string LineName, string Fen, bool WhiteToMove, string Expected);
}
