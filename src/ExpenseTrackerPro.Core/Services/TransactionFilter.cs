using System.Globalization;
using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.Core.Services;

public static class TransactionFilter
{
    /// <summary>Label of the "no category filter" entry in the category combo box.</summary>
    public const string AllCategoriesLabel = "Toate categoriile";

    // Not ro-RO: Romanian collation treats ă, â, î, ș and ț as separate letters, so "sedinta" would not find "Ședință".
    // The root (invariant) collation folds them to their base letters when IgnoreNonSpace is set.
    private static readonly CompareInfo SearchCompare = CultureInfo.InvariantCulture.CompareInfo;

    private const CompareOptions SearchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// Keeps the items whose description contains <paramref name="searchText"/> (case- and diacritic-insensitive,
    /// so "sedinta" finds "Ședință") and whose category equals <paramref name="category"/>. A blank search text or a
    /// null/empty/<see cref="AllCategoriesLabel"/> category does not filter. The original order is preserved.
    /// </summary>
    public static IReadOnlyList<TransactionItem> Apply(
        IEnumerable<TransactionItem> items,
        string? searchText,
        string? category)
    {
        var term = searchText?.Trim() ?? string.Empty;
        var filterByCategory = !string.IsNullOrEmpty(category) && category != AllCategoriesLabel;

        return items
            .Where(x => !filterByCategory || string.Equals(x.Category, category, StringComparison.Ordinal))
            .Where(x => term.Length == 0 || SearchCompare.IndexOf(x.Description, term, SearchOptions) >= 0)
            .ToList();
    }
}
