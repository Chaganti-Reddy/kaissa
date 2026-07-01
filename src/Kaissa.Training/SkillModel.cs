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

    public bool Has(PatternId pattern) => _cards.ContainsKey(pattern);

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
            Cards = _cards.Values.Select(c => new CardDto
            {
                Pattern = c.Pattern.Value,
                Stability = c.State?.Stability,
                Difficulty = c.State?.Difficulty,
                LastReviewUtc = c.LastReviewUtc,
                DueUtc = c.DueUtc,
                Reps = c.Reps,
                Lapses = c.Lapses,
            }).ToList(),
        };

        return JsonSerializer.Serialize(dto);
    }

    public static SkillModel FromJson(string json)
    {
        var model = new SkillModel();
        var dto = JsonSerializer.Deserialize<ModelDto>(json) ?? new ModelDto();
        model.RatingEstimate = dto.RatingEstimate;

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
            };
        }

        return model;
    }

    private sealed class ModelDto
    {
        public double RatingEstimate { get; init; } = RatingEstimator.Default;
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
    }
}
