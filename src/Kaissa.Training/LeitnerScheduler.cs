namespace Kaissa.Training;

/// <summary>One card in a Leitner box system: which box it sits in (1..N) and the day it is next due.</summary>
public readonly record struct LeitnerCard(string Id, int Box, int DueDay)
{
    public static LeitnerCard New(string id) => new(id, 1, 0);
}

/// <summary>
/// A five-box Leitner scheduler - the simple spaced-repetition alternative to FSRS (which stays the
/// default). A correct answer promotes the card one box (longer interval); a wrong answer sends it
/// back to box 1. Days are passed in rather than read from the clock, so scheduling is deterministic
/// and unit-testable. Intervals default to the classic ladder 1/2/4/8/16 days by box.
/// </summary>
public sealed class LeitnerScheduler
{
    private readonly int[] _intervals;

    /// <param name="intervals">Days to wait per box (index 0 = box 1). Length sets the number of boxes.</param>
    public LeitnerScheduler(params int[] intervals)
    {
        _intervals = (intervals is { Length: > 0 }) ? intervals : new[] { 1, 2, 4, 8, 16 };
    }

    public int BoxCount => _intervals.Length;

    /// <summary>Apply a review outcome, returning the card's new box and due day.</summary>
    public LeitnerCard Review(LeitnerCard card, bool correct, int today)
    {
        int box = correct
            ? Math.Min(card.Box + 1, BoxCount)
            : 1;
        int interval = _intervals[box - 1];
        return card with { Box = box, DueDay = today + interval };
    }

    /// <summary>Cards due on or before <paramref name="today"/>, hardest (lowest box) first.</summary>
    public IReadOnlyList<LeitnerCard> Due(IEnumerable<LeitnerCard> cards, int today) =>
        cards.Where(c => c.DueDay <= today)
             .OrderBy(c => c.Box)
             .ThenBy(c => c.Id, StringComparer.Ordinal)
             .ToList();
}
