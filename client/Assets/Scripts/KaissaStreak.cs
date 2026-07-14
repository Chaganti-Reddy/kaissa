using System;
using System.Globalization;

// Tracks a consecutive-days training streak (like the daily streaks players expect). Call
// RecordToday() whenever the player trains; CurrentDays() returns the live streak (0 if it lapsed).
public static class KaissaStreak
{
    public static void RecordToday()
    {
        var today = DateTime.Now.Date;
        var last = Parse(KaissaSettings.LastActive);
        if (last == today)
            return;
        KaissaSettings.DayStreak = last == today.AddDays(-1) ? KaissaSettings.DayStreak + 1 : 1;
        KaissaSettings.LastActive = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static int CurrentDays()
    {
        var today = DateTime.Now.Date;
        var last = Parse(KaissaSettings.LastActive);
        return last == today || last == today.AddDays(-1) ? KaissaSettings.DayStreak : 0;
    }

    private static DateTime Parse(string s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.Date
            : DateTime.MinValue;
}
