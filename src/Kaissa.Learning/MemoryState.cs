namespace Kaissa.Learning;

/// <summary>
/// The FSRS memory state of a single learnable item (in Kaissa, a chess pattern).
/// <para><see cref="Stability"/> is the number of days for recall probability to fall to the
/// desired retention; higher means the pattern is remembered longer.</para>
/// <para><see cref="Difficulty"/> is the item's intrinsic difficulty for this learner, on a
/// 1..10 scale; higher means stability grows more slowly.</para>
/// </summary>
public readonly record struct MemoryState(double Stability, double Difficulty);

/// <summary>The result of scheduling a review: the updated memory state and the next interval.</summary>
public readonly record struct ReviewResult(MemoryState State, int IntervalDays);
