using System.Text.Json;
using Kaissa.Learning;

namespace Kaissa.Training;

/// <summary>Per-player learning state for one pattern: its FSRS memory state plus review history.</summary>
public sealed class PatternCard
{
    public required PatternId Pattern { get; init; }
    public MemoryState? State { get; set; }
    public DateTime? LastReviewUtc { get; set; }
    public DateTime? DueUtc { get; set; }
    public int Reps { get; set; }
    public int Lapses { get; set; }

    /// <summary>The player's estimated strength on this specific pattern, on the puzzle-rating scale.
    /// Selection uses this so each motif is drilled at its own difficulty, not the overall average.</summary>
    public double Rating { get; set; } = RatingEstimator.Default;

    public bool Seen => State is not null;
}

/// <summary>
/// The player's whole learning state: one <see cref="PatternCard"/> per pattern they have met.
/// Serialisable so progress can be saved locally now and synced later.
/// </summary>
public sealed class SkillModel
{
    private readonly Dictionary<PatternId, PatternCard> _cards = new();

    public IReadOnlyCollection<PatternCard> Cards => _cards.Values;

    /// <summary>Live estimate of the player's overall strength on the puzzle-rating scale.</summary>
    public double RatingEstimate { get; set; } = RatingEstimator.Default;

    /// <summary>A separate rating for timed solving (Puzzle Blitz), so fast and slow strength are tracked
    /// apart - a puzzle can be easy-slow but hard-fast, as the dual-rating trainers show.</summary>
    public double BlitzRating { get; set; } = RatingEstimator.Default;

    /// <summary>Updates the timed (blitz) rating after a timed attempt against a puzzle's rating.</summary>
    public void UpdateBlitz(int puzzleRating, bool solved) =>
        BlitzRating = RatingEstimator.Update(BlitzRating, puzzleRating, solved);

    /// <summary>Consecutive correct answers, current and best.</summary>
    public int CurrentStreak { get; private set; }
    public int BestStreak { get; private set; }

    private readonly List<double> _ratingHistory = new();
    public IReadOnlyList<double> RatingHistory => _ratingHistory;

    /// <summary>Records the result of an attempt for streak and rating-history tracking.</summary>
    public void RecordResult(bool correct, double rating)
    {
        if (correct)
        {
            CurrentStreak++;
            if (CurrentStreak > BestStreak)
                BestStreak = CurrentStreak;
        }
        else
        {
            CurrentStreak = 0;
        }

        _ratingHistory.Add(rating);
    }

    public bool Has(PatternId pattern) => _cards.ContainsKey(pattern);

    /// <summary>The pattern's own rating if it has been met, else the overall estimate (a sensible
    /// starting difficulty for a pattern the player has not seen yet).</summary>
    public double PatternRating(PatternId pattern) =>
        _cards.TryGetValue(pattern, out var card) && card.Seen ? card.Rating : RatingEstimate;

    public PatternCard GetOrCreate(PatternId pattern)
    {
        if (!_cards.TryGetValue(pattern, out var card))
            _cards[pattern] = card = new PatternCard { Pattern = pattern };
        return card;
    }

    /// <summary>Seen patterns whose next review is due at or before <paramref name="now"/>.</summary>
    public IEnumerable<PatternCard> Due(DateTime now) =>
        _cards.Values.Where(c => c.DueUtc is { } due && due <= now);

    public string ToJson()
    {
        var dto = new ModelDto
        {
            RatingEstimate = RatingEstimate,
            BlitzRating = BlitzRating,
            CurrentStreak = CurrentStreak,
            BestStreak = BestStreak,
            RatingHistory = _ratingHistory.ToList(),
            Cards = _cards.Values.Select(c => new CardDto
            {
                Pattern = c.Pattern.Value,
                Stability = c.State?.Stability,
                Difficulty = c.State?.Difficulty,
                LastReviewUtc = c.LastReviewUtc,
                DueUtc = c.DueUtc,
                Reps = c.Reps,
                Lapses = c.Lapses,
                Rating = c.Rating,
            }).ToList(),
        };

        return JsonSerializer.Serialize(dto);
    }

    public static SkillModel FromJson(string json)
    {
        var model = new SkillModel();
        var dto = JsonSerializer.Deserialize<ModelDto>(json) ?? new ModelDto();
        model.RatingEstimate = dto.RatingEstimate;
        model.BlitzRating = dto.BlitzRating ?? dto.RatingEstimate; // old saves fall back to the overall rating
        model.CurrentStreak = dto.CurrentStreak;
        model.BestStreak = dto.BestStreak;
        model._ratingHistory.AddRange(dto.RatingHistory);

        foreach (var card in dto.Cards)
        {
            var pattern = new PatternId(card.Pattern);
            model._cards[pattern] = new PatternCard
            {
                Pattern = pattern,
                State = card.Stability is { } s && card.Difficulty is { } d ? new MemoryState(s, d) : null,
                LastReviewUtc = card.LastReviewUtc,
                DueUtc = card.DueUtc,
                Reps = card.Reps,
                Lapses = card.Lapses,
                // Saves from before per-pattern ratings existed have no value; start such a pattern
                // from the overall estimate so a calibrated player isn't reset to the default.
                Rating = card.Rating ?? model.RatingEstimate,
            };
        }

        return model;
    }

    private sealed class ModelDto
    {
        public double RatingEstimate { get; init; } = RatingEstimator.Default;
        public double? BlitzRating { get; init; } // nullable so pre-existing saves fall back to the overall rating
        public int CurrentStreak { get; init; }
        public int BestStreak { get; init; }
        public List<double> RatingHistory { get; init; } = new();
        public List<CardDto> Cards { get; init; } = new();
    }

    private sealed class CardDto
    {
        public string Pattern { get; init; } = "";
        public double? Stability { get; init; }
        public double? Difficulty { get; init; }
        public DateTime? LastReviewUtc { get; init; }
        public DateTime? DueUtc { get; init; }
        public int Reps { get; init; }
        public int Lapses { get; init; }
        public double? Rating { get; init; } // nullable so pre-existing saves fall back to the overall rating
    }
}
