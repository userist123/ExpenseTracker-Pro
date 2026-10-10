using System.Globalization;
using System.Text.RegularExpressions;

namespace ExpenseTrackerPro.Core.Services;

/// <summary>
/// Parses a user-typed amount.
/// <para>
/// Rules: whitespace (including non-breaking spaces) is ignored; only digits and at most one decimal
/// separator (',' or '.') with 1-2 decimals are accepted. Thousands separators are NOT supported, so
/// "12.50" is 12.50 (never 1250), "1,234" is rejected (three decimals) instead of being guessed, and any
/// input that contains both '.' and ',' (e.g. "1.234,56") is rejected as ambiguous.
/// Signs are rejected. The value must be in the range (0, <see cref="MaxAmount"/>].
/// </para>
/// </summary>
public static partial class AmountParser
{
    public const decimal MaxAmount = 10_000_000m;

    public static bool TryParse(string? input, out decimal amount)
    {
        amount = 0m;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = RemoveWhitespace(input);
        if (!AmountPattern().IsMatch(normalized))
        {
            return false;
        }

        normalized = normalized.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        if (parsed <= 0m || parsed > MaxAmount)
        {
            return false;
        }

        amount = parsed;
        return true;
    }

    private static string RemoveWhitespace(string input)
    {
        return string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
    }

    [GeneratedRegex("^[0-9]+([.,][0-9]{1,2})?$")]
    private static partial Regex AmountPattern();
}
