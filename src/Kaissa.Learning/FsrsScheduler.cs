namespace Kaissa.Learning;

/// <summary>
/// A faithful port of the FSRS-6 scheduling math (see the reference implementation
/// open-spaced-repetition/py-fsrs). Given a pattern's memory state, the time since its last
/// review, and a grade, it returns the updated state and the next interval.
/// <para>This is the long-/short-term stability core only. Anki-style multi-step "learning" and
/// "relearning" queues and interval fuzzing are intentionally omitted: Kaissa schedules patterns,
/// not flashcards, and does not need them.</para>
/// </summary>
public sealed class FsrsScheduler
{
    private const double MinDifficulty = 1.0;
    private const double MaxDifficulty = 10.0;
    private const double MinStability = 0.001;

    private readonly FsrsParameters _p;

    public FsrsScheduler(FsrsParameters? parameters = null) => _p = parameters ?? new FsrsParameters();

    public FsrsParameters Parameters => _p;

    /// <summary>Memory state after the very first review of a new pattern.</summary>
    public MemoryState InitialState(Rating rating) => new(
        ClampStability(_p[(int)rating - 1]),
        ClampDifficulty(InitialDifficulty(rating)));

    /// <summary>Probability the pattern is recalled after <paramref name="elapsedDays"/> since last review.</summary>
    public double Retrievability(MemoryState state, double elapsedDays)
    {
        if (elapsedDays <= 0)
            return 1.0;
        return Math.Pow(1.0 + _p.Factor * elapsedDays / state.Stability, _p.Decay);
    }

    /// <summary>Days until the pattern should next be reviewed to hit the desired retention.</summary>
    public int NextIntervalDays(double stability)
    {
        var interval = stability / _p.Factor * (Math.Pow(_p.DesiredRetention, 1.0 / _p.Decay) - 1.0);
        return Math.Clamp((int)Math.Round(interval), 1, _p.MaximumIntervalDays);
    }

    public int NextIntervalDays(MemoryState state) => NextIntervalDays(state.Stability);

    /// <summary>Updates the memory state for a review of an existing pattern.</summary>
    public MemoryState NextState(MemoryState current, double elapsedDays, Rating rating)
    {
        double stability = elapsedDays < 1.0
            ? ShortTermStability(current.Stability, rating)
            : NextStability(current, Retrievability(current, elapsedDays), rating);
        double difficulty = NextDifficulty(current.Difficulty, rating);
        return new MemoryState(ClampStability(stability), ClampDifficulty(difficulty));
    }

    /// <summary>
    /// Schedules a review. Pass <c>null</c> for a pattern's first-ever review;
    /// otherwise pass its current state and the days since it was last reviewed.
    /// </summary>
    public ReviewResult Review(MemoryState? current, double elapsedDays, Rating rating)
    {
        var next = current is { } state ? NextState(state, elapsedDays, rating) : InitialState(rating);
        return new ReviewResult(next, NextIntervalDays(next.Stability));
    }

    private double InitialDifficulty(Rating rating) =>
        _p[4] - Math.Exp(_p[5] * ((int)rating - 1)) + 1.0;

    private double ShortTermStability(double stability, Rating rating)
    {
        double increase = Math.Exp(_p[17] * ((int)rating - 3 + _p[18])) * Math.Pow(stability, -_p[19]);
        if (rating is Rating.Good or Rating.Easy)
            increase = Math.Max(increase, 1.0);
        return stability * increase;
    }

    private double NextDifficulty(double difficulty, Rating rating)
    {
        double deltaDifficulty = -(_p[6] * ((int)rating - 3));
        double linearDamped = difficulty + (10.0 - difficulty) * deltaDifficulty / 9.0;
        double reversionTarget = InitialDifficulty(Rating.Easy); // unclamped, per FSRS
        return _p[7] * reversionTarget + (1.0 - _p[7]) * linearDamped;
    }

    private double NextStability(MemoryState state, double retrievability, Rating rating) =>
        rating == Rating.Again
            ? NextForgetStability(state, retrievability)
            : NextRecallStability(state, retrievability, rating);

    private double NextRecallStability(MemoryState state, double retrievability, Rating rating)
    {
        double hardPenalty = rating == Rating.Hard ? _p[15] : 1.0;
        double easyBonus = rating == Rating.Easy ? _p[16] : 1.0;

        return state.Stability * (1.0
            + Math.Exp(_p[8])
            * (11.0 - state.Difficulty)
            * Math.Pow(state.Stability, -_p[9])
            * (Math.Exp((1.0 - retrievability) * _p[10]) - 1.0)
            * hardPenalty
            * easyBonus);
    }

    private double NextForgetStability(MemoryState state, double retrievability)
    {
        double longTerm = _p[11]
            * Math.Pow(state.Difficulty, -_p[12])
            * (Math.Pow(state.Stability + 1.0, _p[13]) - 1.0)
            * Math.Exp((1.0 - retrievability) * _p[14]);

        double shortTerm = state.Stability / Math.Exp(_p[17] * _p[18]);

        return Math.Min(longTerm, shortTerm);
    }

    private static double ClampStability(double stability) => Math.Max(stability, MinStability);

    private static double ClampDifficulty(double difficulty) =>
        Math.Clamp(difficulty, MinDifficulty, MaxDifficulty);
}
