using System.Globalization;

namespace ExpenseTrackerPro.Core.Services;

public static class MonthFormatter
{
    private static readonly CultureInfo Romanian = CultureInfo.GetCultureInfo("ro-RO");

    /// <summary>Romanian month title with a capital first letter, e.g. "Octombrie 2026".</summary>
    public static string Title(DateTime month)
    {
        var text = month.ToString("MMMM yyyy", Romanian);
        return char.ToUpper(text[0], Romanian) + text[1..];
    }

    /// <summary>The first day of the month that contains <paramref name="date"/>.</summary>
    public static DateTime FirstOfMonth(DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1);
    }
}
