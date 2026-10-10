using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public class ExpenseBreakdownTests
{
    [Fact]
    public void Empty_input_returns_an_empty_list()
    {
        Assert.Empty(ExpenseBreakdown.From([]));
    }

    [Fact]
    public void Income_only_returns_an_empty_list_without_dividing_by_zero()
    {
        var result = ExpenseBreakdown.From([TestItems.Income("Salariu", "Salariu", 5000m)]);

        Assert.Empty(result);
    }

    [Fact]
    public void Groups_by_category_orders_largest_first_and_computes_shares()
    {
        var result = ExpenseBreakdown.From(
        [
            TestItems.Expense("Transport", "Metrou", 100m),
            TestItems.Expense("Alimentație", "Piață", 300m),
            TestItems.Expense("Alimentație", "Magazin", 200m),
            TestItems.Expense("Utilități", "Curent", 400m),
            TestItems.Income("Salariu", "Salariu", 9000m),
        ]);

        Assert.Equal(["Alimentație", "Utilități", "Transport"], result.Select(x => x.Category));
    }

    [Fact]
    public void Shares_are_fractions_that_sum_to_one()
    {
        var result = ExpenseBreakdown.From(
        [
            TestItems.Expense("Transport", "Metrou", 100m),
            TestItems.Expense("Alimentație", "Piață", 300m),
            TestItems.Expense("Utilități", "Curent", 600m),
        ]);

        Assert.Equal(["Utilități", "Alimentație", "Transport"], result.Select(x => x.Category));
        Assert.Equal([600m, 300m, 100m], result.Select(x => x.Amount));
        Assert.Equal(0.6, result[0].Share, 6);
        Assert.Equal(0.3, result[1].Share, 6);
        Assert.Equal(0.1, result[2].Share, 6);
        Assert.Equal(1d, result.Sum(x => x.Share), 6);
    }

    [Fact]
    public void A_single_category_has_a_full_share()
    {
        var result = ExpenseBreakdown.From(
        [
            TestItems.Expense("Altele", "A", 10m),
            TestItems.Expense("Altele", "B", 5m),
        ]);

        var only = Assert.Single(result);
        Assert.Equal(15m, only.Amount);
        Assert.Equal(1d, only.Share);
    }

    [Fact]
    public void Equal_amounts_are_ordered_by_category_name()
    {
        var result = ExpenseBreakdown.From(
        [
            TestItems.Expense("Transport", "A", 50m),
            TestItems.Expense("Alimentație", "B", 50m),
        ]);

        Assert.Equal(["Alimentație", "Transport"], result.Select(x => x.Category));
    }

    [Fact]
    public void Service_returns_the_breakdown_of_the_requested_month_only()
    {
        using var database = new TestDatabase();
        var service = new TransactionService(database.Factory, new SettingsService(database.Factory));
        service.Add(TransactionType.Expense, "Transport", "Bilet", 30m, new DateTime(2026, 10, 3));
        service.Add(TransactionType.Expense, "Alimentație", "Piață", 90m, new DateTime(2026, 10, 4));
        service.Add(TransactionType.Expense, "Alimentație", "Luna viitoare", 500m, new DateTime(2026, 11, 1));
        service.Add(TransactionType.Income, "Salariu", "Salariu", 5000m, new DateTime(2026, 10, 1));

        var result = service.GetExpensesByCategory(2026, 10);

        Assert.Equal(["Alimentație", "Transport"], result.Select(x => x.Category));
        Assert.Equal(0.75, result[0].Share, 6);
        Assert.Empty(service.GetExpensesByCategory(2026, 9));
    }
}
