namespace Kaissa.Training;

/// <summary>Picks a stable "puzzle of the day": everyone gets the same one on a given date.</summary>
public static class DailyPuzzle
{
    public static Scenario ForDate(ScenarioLibrary library, DateTime date)
    {
        var all = library.AllScenarios.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
        if (all.Count == 0)
            throw new InvalidOperationException("No scenarios available.");

        // Deterministic index from the calendar date.
        long seed = date.Year * 10000L + date.Month * 100L + date.Day;
        int index = (int)(((seed % all.Count) + all.Count) % all.Count);
        return all[index];
    }
}
