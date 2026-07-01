namespace Kaissa.Learning;

/// <summary>
/// Parameters for the FSRS-6 scheduler: the 21 model weights plus the target retention and an
/// interval cap. Defaults are the published FSRS-6 defaults (trained on a large public dataset).
/// A later phase can fit these weights per player from their own review history; the scheduler
/// code does not change when that happens.
/// </summary>
public sealed class FsrsParameters
{
    /// <summary>Published FSRS-6 default weights (w0..w20).</summary>
    public static IReadOnlyList<double> DefaultWeights { get; } = new[]
    {
        0.212, 1.2931, 2.3065, 8.2956, 6.4133, 0.8334, 3.0194, 0.001, 1.8722, 0.1666,
        0.796, 1.4835, 0.0614, 0.2629, 1.6483, 0.6014, 1.8729, 0.5425, 0.0912, 0.0658, 0.1542,
    };

    public const int WeightCount = 21;

    public IReadOnlyList<double> Weights { get; }

    /// <summary>Target probability of recall at review time (FSRS default 0.9).</summary>
    public double DesiredRetention { get; }

    /// <summary>Upper bound on any scheduled interval, in days.</summary>
    public int MaximumIntervalDays { get; }

    /// <summary>FSRS decay exponent, derived from w20.</summary>
    public double Decay { get; }

    /// <summary>FSRS interval factor, derived from the decay.</summary>
    public double Factor { get; }

    public FsrsParameters(
        IReadOnlyList<double>? weights = null,
        double desiredRetention = 0.9,
        int maximumIntervalDays = 36_500)
    {
        Weights = weights ?? DefaultWeights;
        if (Weights.Count != WeightCount)
            throw new ArgumentException($"FSRS requires exactly {WeightCount} weights.", nameof(weights));
        if (desiredRetention is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(desiredRetention), "Must be in (0, 1).");
        if (maximumIntervalDays < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumIntervalDays), "Must be at least 1.");

        DesiredRetention = desiredRetention;
        MaximumIntervalDays = maximumIntervalDays;
        Decay = -Weights[20];
        Factor = Math.Pow(0.9, 1.0 / Decay) - 1.0;
    }

    public double this[int index] => Weights[index];
}
