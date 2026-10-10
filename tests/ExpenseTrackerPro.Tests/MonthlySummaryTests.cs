using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public class MonthlySummaryTests
{
    [Fact]
    public void From_computes_totals_from_the_given_items()
    {
        var summary = MonthlySummary.From(
        [
            TestItems.Income("Salariu", "Salariu", 8600m),
            TestItems.Expense("Utilități", "Chirie", 2350m),
            TestItems.Expense("Alimentație", "Piață", 420.50m),
        ],
        7500m);

        Assert.Equal(new MonthlySummary(8600m, 2770.50m, 5829.50m, 7500m, 4729.50m), summary);
    }

    [Fact]
    public void From_of_no_items_is_zero_with_the_full_budget()
    {
        Assert.Equal(new MonthlySummary(0m, 0m, 0m, 7500m, 7500m), MonthlySummary.From([], 7500m));
    }

    [Fact]
    public void Service_overload_matches_the_query_based_summary()
    {
        using var database = new TestDatabase();
        var settings = new SettingsService(database.Factory);
        var service = new TransactionService(database.Factory, settings);
        settings.SetMonthlyBudget(3000m);
        service.Add(TransactionType.Income, "Salariu", "Salariu", 4000m, new DateTime(2026, 10, 1));
        service.Add(TransactionType.Expense, "Transport", "Benzină", 500m, new DateTime(2026, 10, 2));

        var items = service.GetMonth(2026, 10);

        Assert.Equal(service.GetSummary(2026, 10), service.GetSummary(items));
        Assert.Equal(2500m, service.GetSummary(items).RemainingBudget);
    }

    [Fact]
    public void BudgetUsage_is_the_share_of_the_budget_spent()
    {
        var summary = MonthlySummary.From([TestItems.Expense("Altele", "x", 750m)], 3000m);

        Assert.Equal(0.25, summary.BudgetUsage, 6);
        Assert.False(summary.IsOverBudget);
    }

    [Fact]
    public void BudgetUsage_is_clamped_to_one_when_over_budget()
    {
        var summary = MonthlySummary.From([TestItems.Expense("Altele", "x", 5000m)], 3000m);

        Assert.Equal(1d, summary.BudgetUsage);
        Assert.True(summary.IsOverBudget);
        Assert.Equal(-2000m, summary.RemainingBudget);
    }

    [Fact]
    public void BudgetUsage_is_zero_without_expenses_or_without_a_budget()
    {
        Assert.Equal(0d, MonthlySummary.From([], 3000m).BudgetUsage);
        Assert.Equal(0d, MonthlySummary.From([TestItems.Expense("Altele", "x", 10m)], 0m).BudgetUsage);
    }

    [Fact]
    public void Spending_exactly_the_budget_is_not_over_budget()
    {
        var summary = MonthlySummary.From([TestItems.Expense("Altele", "x", 3000m)], 3000m);

        Assert.False(summary.IsOverBudget);
        Assert.Equal(1d, summary.BudgetUsage);
    }

    [Fact]
    public void Balance_is_negative_only_when_expenses_exceed_income()
    {
        var negative = MonthlySummary.From([TestItems.Income("Salariu", "s", 100m), TestItems.Expense("Altele", "x", 150m)], 7500m);
        var zero = MonthlySummary.From([TestItems.Income("Salariu", "s", 100m), TestItems.Expense("Altele", "x", 100m)], 7500m);

        Assert.True(negative.IsBalanceNegative);
        Assert.False(zero.IsBalanceNegative);
    }
}
