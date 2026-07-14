namespace Kaissa.Training.Play;

/// <summary>Position features that drive how long a human-like opponent would think here.</summary>
public readonly record struct MoveTimeContext(
    bool IsBook,             // the move is still opening theory
    bool IsRecapture,        // the move recaptures on the square the opponent just used
    bool IsForced,           // only one legal move
    int LegalMoveCount,      // branching / complexity proxy
    double? RemainingSeconds, // the mover's clock, or null for an untimed game
    double IncrementSeconds,
    int Tempo);              // 0 snappy, 1 normal, 2 deliberate

/// <summary>
/// A think-time model for the computer opponent, so it spends time like a person rather than replying
/// instantly. It plays book moves and recaptures quickly, tanks on complex positions, jitters so it is
/// never metronomic, and manages a clock (moving faster when low on time). The caller supplies a random
/// 0..1 so the model stays pure and testable; it decides the delay, not the engine's search depth.
/// </summary>
public static class MoveTimeModel
{
    public static double ThinkSeconds(MoveTimeContext c, double rand01)
    {
        rand01 = Math.Clamp(rand01, 0.0, 1.0);

        // Quick moves get a small absolute time; only genuine decisions scale with the clock, so a
        // recapture stays snappy even early when there is lots of time on the clock.
        double t;
        if (c.IsBook)
            t = 0.25 + 0.25 * rand01;
        else if (c.IsForced || c.LegalMoveCount <= 1)
            t = 0.35 + 0.35 * rand01;
        else if (c.IsRecapture)
            t = 0.45 + 0.40 * rand01;
        else
        {
            double alloc = c.RemainingSeconds is double rem ? rem / 30.0 + c.IncrementSeconds * 0.8 : 1.2;
            double factor = c.LegalMoveCount >= 35 ? 1.70 : c.LegalMoveCount >= 22 ? 1.15 : 0.80;
            t = alloc * factor * (0.7 + 0.6 * rand01); // +/- 30% jitter
        }
        t *= c.Tempo switch { 0 => 0.65, 2 => 1.5, _ => 1.0 };

        // Clock management: move fast when low, and never sink a big share into one move.
        if (c.RemainingSeconds is double r)
        {
            if (r < 10) t = Math.Min(t, 0.4 + 0.5 * rand01);
            else if (r < 30) t = Math.Min(t, r * 0.06);
            t = Math.Min(t, r * 0.20);
        }

        double min = c.IsBook ? 0.2 : 0.3;
        return Math.Clamp(t, min, 6.0);
    }
}
