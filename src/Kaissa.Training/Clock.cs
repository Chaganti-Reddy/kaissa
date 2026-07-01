namespace Kaissa.Training;

/// <summary>Supplies the current time. Abstracted so training logic can be tested deterministically.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>Real wall-clock time.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>A clock the test/simulation harness can advance by hand.</summary>
public sealed class ManualClock : IClock
{
    public ManualClock(DateTime start) => UtcNow = start;

    public DateTime UtcNow { get; private set; }

    public void Advance(TimeSpan by) => UtcNow += by;

    public void AdvanceDays(double days) => UtcNow += TimeSpan.FromDays(days);
}
