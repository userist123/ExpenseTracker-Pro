using System.Globalization;
using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.Core.Services;

public static class ExpenseBreakdown
{
    private static readonly StringComparer CategoryComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("ro-RO"), ignoreCase: false);

    /// <summary>
    /// Groups the expenses (income is ignored) by category, largest first; equal amounts are ordered by category name.
    /// Returns an empty list when there are no expenses, so callers never divide by zero.
    /// </summary>
    public static IReadOnlyList<CategoryExpense> From(IEnumerable<TransactionItem> items)
    {
        var totals = items
            .Where(x => x.Type == TransactionType.Expense)
            .GroupBy(x => x.Category)
            .Select(group => (Category: group.Key, Amount: group.Sum(x => x.Amount)))
            .ToList();

        var total = totals.Sum(x => x.Amount);
        if (total <= 0m)
        {
            return [];
        }

        return totals
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.Category, CategoryComparer)
            .Select(x => new CategoryExpense(x.Category, x.Amount, (double)(x.Amount / total)))
            .ToList();
    }
}
