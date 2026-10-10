using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void Budget_defaults_to_7500_when_unset()
    {
        using var database = new TestDatabase();
        var settings = new SettingsService(database.Factory);

        Assert.Equal(7500m, settings.GetMonthlyBudget());
    }

    [Fact]
    public void Budget_roundtrips_and_can_be_overwritten()
    {
        using var database = new TestDatabase();
        var settings = new SettingsService(database.Factory);

        Assert.True(settings.SetMonthlyBudget(4200.50m).Success);
        Assert.Equal(4200.50m, settings.GetMonthlyBudget());

        Assert.True(settings.SetMonthlyBudget(9000m).Success);
        Assert.Equal(9000m, settings.GetMonthlyBudget());
    }

    [Fact]
    public void Budget_is_stored_with_the_invariant_culture()
    {
        using var database = new TestDatabase();
        var settings = new SettingsService(database.Factory);
        settings.SetMonthlyBudget(1234.5m);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT \"Value\" FROM \"AppSettings\" WHERE \"Key\" = 'MonthlyBudget';";

        Assert.Equal("1234.5", command.ExecuteScalar());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10_000_001)]
    public void Invalid_budget_is_rejected_and_not_saved(int budget)
    {
        using var database = new TestDatabase();
        var settings = new SettingsService(database.Factory);

        var result = settings.SetMonthlyBudget(budget);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(SettingsService.DefaultMonthlyBudget, settings.GetMonthlyBudget());
    }
}
