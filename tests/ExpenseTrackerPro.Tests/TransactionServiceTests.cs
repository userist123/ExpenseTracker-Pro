using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public sealed class TransactionServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly SettingsService _settings;
    private readonly TransactionService _service;

    public TransactionServiceTests()
    {
        _settings = new SettingsService(_database.Factory);
        _service = new TransactionService(_database.Factory, _settings);
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    [Fact]
    public void Add_saves_a_normalized_transaction()
    {
        var result = _service.Add(
            TransactionType.Expense,
            "Alimentație",
            "  Cumpărături  ",
            12.504m,
            new DateTime(2026, 10, 5, 17, 30, 0));

        Assert.True(result.Success);
        var saved = Assert.Single(_service.GetMonth(2026, 10));
        Assert.Equal(result.Value!.Id, saved.Id);
        Assert.Equal("Cumpărături", saved.Description);
        Assert.Equal(12.50m, saved.Amount);
        Assert.Equal(new DateTime(2026, 10, 5), saved.Date);
        Assert.Equal(TransactionType.Expense, saved.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_rejects_empty_description(string description)
    {
        var result = _service.Add(TransactionType.Expense, "Altele", description, 10m, new DateTime(2026, 10, 5));

        Assert.False(result.Success);
        Assert.Contains("descriere", result.Error);
        Assert.Empty(_service.GetMonth(2026, 10));
    }

    [Fact]
    public void Add_accepts_a_description_of_120_characters_and_rejects_121()
    {
        var ok = _service.Add(TransactionType.Expense, "Altele", new string('a', 120), 10m, new DateTime(2026, 10, 5));
        var tooLong = _service.Add(TransactionType.Expense, "Altele", new string('a', 121), 10m, new DateTime(2026, 10, 5));

        Assert.True(ok.Success);
        Assert.False(tooLong.Success);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_000_001)]
    public void Add_rejects_amounts_out_of_range(int amount)
    {
        var result = _service.Add(TransactionType.Expense, "Altele", "Test", amount, new DateTime(2026, 10, 5));

        Assert.False(result.Success);
        Assert.Contains("Suma", result.Error);
    }

    [Fact]
    public void Add_rejects_amount_that_rounds_to_zero()
    {
        var result = _service.Add(TransactionType.Expense, "Altele", "Test", 0.001m, new DateTime(2026, 10, 5));

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(TransactionType.Expense, "Salariu")]
    [InlineData(TransactionType.Income, "Transport")]
    [InlineData(TransactionType.Expense, "Inexistentă")]
    public void Add_rejects_category_not_valid_for_type(TransactionType type, string category)
    {
        var result = _service.Add(type, category, "Test", 10m, new DateTime(2026, 10, 5));

        Assert.False(result.Success);
        Assert.Contains("Categoria", result.Error);
        Assert.Empty(_service.GetMonth(2026, 10));
    }

    [Fact]
    public void GetMonth_respects_month_boundaries_and_orders_newest_first()
    {
        AddExpense("30 sept", new DateTime(2026, 9, 30));
        AddExpense("1 oct", new DateTime(2026, 10, 1));
        AddExpense("31 oct", new DateTime(2026, 10, 31));
        AddExpense("1 nov", new DateTime(2026, 11, 1));

        var october = _service.GetMonth(2026, 10);

        Assert.Equal(["31 oct", "1 oct"], october.Select(x => x.Description));
    }

    [Fact]
    public void GetMonth_handles_the_december_to_january_boundary()
    {
        AddExpense("31 dec", new DateTime(2026, 12, 31));
        AddExpense("1 ian", new DateTime(2027, 1, 1));

        Assert.Equal(["31 dec"], _service.GetMonth(2026, 12).Select(x => x.Description));
        Assert.Equal(["1 ian"], _service.GetMonth(2027, 1).Select(x => x.Description));
    }

    [Fact]
    public void GetSummary_totals_only_the_requested_month()
    {
        _service.Add(TransactionType.Income, "Salariu", "Salariu", 8600m, new DateTime(2026, 10, 5));
        _service.Add(TransactionType.Expense, "Utilități", "Chirie", 2350m, new DateTime(2026, 10, 7));
        _service.Add(TransactionType.Expense, "Alimentație", "Piață", 420.50m, new DateTime(2026, 10, 31));
        _service.Add(TransactionType.Expense, "Alimentație", "Luna viitoare", 999m, new DateTime(2026, 11, 1));
        _service.Add(TransactionType.Income, "Freelance", "Luna trecută", 999m, new DateTime(2026, 9, 30));

        var summary = _service.GetSummary(2026, 10);

        Assert.Equal(8600m, summary.Income);
        Assert.Equal(2770.50m, summary.Expenses);
        Assert.Equal(5829.50m, summary.Balance);
        Assert.Equal(7500m, summary.Budget);
        Assert.Equal(4729.50m, summary.RemainingBudget);
    }

    [Fact]
    public void GetSummary_of_an_empty_month_is_zero_with_full_budget()
    {
        var summary = _service.GetSummary(2026, 10);

        Assert.Equal(new MonthlySummary(0m, 0m, 0m, 7500m, 7500m), summary);
    }

    [Fact]
    public void GetSummary_uses_the_configured_budget()
    {
        _settings.SetMonthlyBudget(3000m);
        _service.Add(TransactionType.Expense, "Transport", "Benzină", 500m, new DateTime(2026, 10, 2));

        var summary = _service.GetSummary(2026, 10);

        Assert.Equal(3000m, summary.Budget);
        Assert.Equal(2500m, summary.RemainingBudget);
    }

    [Fact]
    public void Delete_removes_the_transaction()
    {
        var id = AddExpense("De șters", new DateTime(2026, 10, 5));

        var result = _service.Delete(id);

        Assert.True(result.Success);
        Assert.Empty(_service.GetMonth(2026, 10));
    }

    [Fact]
    public void Delete_of_a_missing_transaction_fails_with_a_message()
    {
        var result = _service.Delete(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Update_changes_the_transaction_and_keeps_the_same_id()
    {
        var id = AddExpense("Vechi", new DateTime(2026, 10, 5));

        var result = _service.Update(id, TransactionType.Income, "Freelance", " Nou ", 300m, new DateTime(2026, 10, 9));

        Assert.True(result.Success);
        var saved = Assert.Single(_service.GetMonth(2026, 10));
        Assert.Equal(id, saved.Id);
        Assert.Equal(TransactionType.Income, saved.Type);
        Assert.Equal("Freelance", saved.Category);
        Assert.Equal("Nou", saved.Description);
        Assert.Equal(300m, saved.Amount);
        Assert.Equal(new DateTime(2026, 10, 9), saved.Date);
    }

    [Fact]
    public void Update_applies_the_same_validation_as_add()
    {
        var id = AddExpense("Vechi", new DateTime(2026, 10, 5));

        var result = _service.Update(id, TransactionType.Expense, "Salariu", "Nou", 300m, new DateTime(2026, 10, 9));

        Assert.False(result.Success);
        Assert.Equal("Vechi", Assert.Single(_service.GetMonth(2026, 10)).Description);
    }

    [Fact]
    public void Update_of_a_missing_transaction_fails()
    {
        var result = _service.Update(Guid.NewGuid(), TransactionType.Expense, "Altele", "Nou", 10m, new DateTime(2026, 10, 9));

        Assert.False(result.Success);
    }

    private Guid AddExpense(string description, DateTime date)
    {
        var result = _service.Add(TransactionType.Expense, "Altele", description, 10m, date);
        Assert.True(result.Success, result.Error);
        return result.Value!.Id;
    }
}
