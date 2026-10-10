using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.Core.Services;

public sealed record MonthlySummary(
    decimal Income,
    decimal Expenses,
    decimal Balance,
    decimal Budget,
    decimal RemainingBudget)
{
    /// <summary>True when the month's expenses exceed the budget.</summary>
    public bool IsOverBudget => RemainingBudget < 0m;

    /// <summary>True when the month's expenses exceed its income.</summary>
    public bool IsBalanceNegative => Balance < 0m;

    /// <summary>Share of the budget already spent, clamped to 0-1 (0 when there is no budget).</summary>
    public double BudgetUsage
    {
        get
        {
            if (Budget <= 0m)
            {
                return 0d;
            }

            var usage = (double)(Expenses / Budget);
            return Math.Clamp(usage, 0d, 1d);
        }
    }

    /// <summary>Computes the totals of the given items (all assumed to belong to one month).</summary>
    public static MonthlySummary From(IEnumerable<TransactionItem> items, decimal budget)
    {
        var income = 0m;
        var expenses = 0m;

        foreach (var item in items)
        {
            if (item.Type == TransactionType.Income)
            {
                income += item.Amount;
            }
            else
            {
                expenses += item.Amount;
            }
        }

        return new MonthlySummary(income, expenses, income - expenses, budget, budget - expenses);
    }
}
