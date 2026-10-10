using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public class AmountParserTests
{
    [Theory]
    [InlineData("12.50", "12.50")] // regression: used to be parsed as 1250 with ro-RO grouping
    [InlineData("12,5", "12.5")]
    [InlineData("12,50", "12.50")]
    [InlineData("1250", "1250")]
    [InlineData("0,01", "0.01")]
    [InlineData("0.5", "0.5")]
    [InlineData("  45  ", "45")]
    [InlineData("1 250", "1250")]
    [InlineData("1 250,75", "1250.75")] // non-breaking space
    [InlineData("10000000", "10000000")]
    public void TryParse_accepts_valid_amounts(string input, string expected)
    {
        var parsed = AmountParser.TryParse(input, out var amount);

        Assert.True(parsed);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("0")]
    [InlineData("0,00")]
    [InlineData("1,234")] // three decimals: rejected instead of guessing a thousands separator
    [InlineData("1.234")]
    [InlineData("1.234,56")] // both separators: ambiguous
    [InlineData("1,234.56")]
    [InlineData("1.234.567")]
    [InlineData("1,2,3")]
    [InlineData("12,")]
    [InlineData("12.")]
    [InlineData(",5")]
    [InlineData("1e3")]
    [InlineData("12 RON")]
    [InlineData("10000000,01")]
    [InlineData("99999999999999999999999999999999")]
    [InlineData("١٢")] // non-ASCII digits
    public void TryParse_rejects_invalid_amounts(string? input)
    {
        var parsed = AmountParser.TryParse(input, out var amount);

        Assert.False(parsed);
        Assert.Equal(0m, amount);
    }
}
