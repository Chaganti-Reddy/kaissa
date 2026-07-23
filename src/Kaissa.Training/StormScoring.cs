namespace Kaissa.Training;

/// <summary>
/// The combo/time bookkeeping for a Puzzle Storm run (Lichess model): a fixed time budget that a
/// consecutive-solve combo tops up, and a per-miss time penalty that also breaks the combo. The
/// clock itself is ticked by the client; this type owns the scoring so it can be unit-tested and
/// shared with a server. Bonus thresholds and amounts are our tuning (documented), not a claim of
/// Lichess's exact figures - the shape (combo bar fills, milestones grant seconds) is the model.
/// </summary>
public sealed class StormScoring
{
    private readonly double _wrongPenalty;
    private readonly int _comboPerBonus;
    private readonly double _comboBonus;

    /// <param name="startSeconds">Initial time budget (Lichess Storm is three minutes).</param>
    /// <param name="wrongPenalty">Seconds lost on a miss.</param>
    /// <param name="comboPerBonus">Consecutive solves per bonus milestone.</param>
    /// <param name="comboBonus">Seconds granted at each milestone.</param>
    public StormScoring(double startSeconds = 180, double wrongPenalty = 10, int comboPerBonus = 5, double comboBonus = 10)
    {
        if (comboPerBonus < 1) throw new ArgumentOutOfRangeException(nameof(comboPerBonus));
        TimeRemaining = startSeconds;
        _wrongPenalty = wrongPenalty;
        _comboPerBonus = comboPerBonus;
        _comboBonus = comboBonus;
    }

    public double TimeRemaining { get; private set; }
    public int Solved { get; private set; }
    public int Missed { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    public int BonusesEarned { get; private set; }

    /// <summary>True once the time budget is spent.</summary>
    public bool IsOver => TimeRemaining <= 0;

    /// <summary>How many more solves until the next bonus milestone (0 when a bonus just landed).</summary>
    public int ToNextBonus => (_comboPerBonus - Combo % _comboPerBonus) % _comboPerBonus;

    /// <summary>Advance the clock by the elapsed real time (client tick). Clamps at zero.</summary>
    public void Tick(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0) return;
        TimeRemaining = Math.Max(0, TimeRemaining - elapsedSeconds);
    }

    /// <summary>Record a solved puzzle: grows the combo and grants bonus time at each milestone.</summary>
    public void OnSolve()
    {
        if (IsOver) return;
        Solved++;
        Combo++;
        if (Combo > BestCombo) BestCombo = Combo;
        if (Combo % _comboPerBonus == 0)
        {
            TimeRemaining += _comboBonus;
            BonusesEarned++;
        }
    }

    /// <summary>Record a miss: costs time and resets the combo. Never pushes the clock below zero.</summary>
    public void OnMiss()
    {
        if (IsOver) return;
        Missed++;
        Combo = 0;
        TimeRemaining = Math.Max(0, TimeRemaining - _wrongPenalty);
    }
}
