using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public class MonthFormatterTests
{
    [Theory]
    [InlineData(2026, 1, "Ianuarie 2026")]
    [InlineData(2026, 2, "Februarie 2026")]
    [InlineData(2026, 8, "August 2026")]
    [InlineData(2026, 10, "Octombrie 2026")]
    [InlineData(2025, 12, "Decembrie 2025")]
    public void Title_is_romanian_with_a_capital_letter(int year, int month, string expected)
    {
        Assert.Equal(expected, MonthFormatter.Title(new DateTime(year, month, 1)));
    }

    [Fact]
    public void FirstOfMonth_drops_the_day_and_time()
    {
        Assert.Equal(new DateTime(2026, 10, 1), MonthFormatter.FirstOfMonth(new DateTime(2026, 10, 27, 15, 30, 0)));
    }
}
