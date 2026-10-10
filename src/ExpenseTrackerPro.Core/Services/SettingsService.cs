using System.Globalization;
using ExpenseTrackerPro.Core.Data;
using ExpenseTrackerPro.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerPro.Core.Services;

public sealed class SettingsService
{
    public const string MonthlyBudgetKey = "MonthlyBudget";
    public const decimal DefaultMonthlyBudget = 7500m;

    private readonly IDbContextFactory<AppDbContext> _factory;

    public SettingsService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Returns the stored monthly budget, or <see cref="DefaultMonthlyBudget"/> when unset or unreadable.</summary>
    public decimal GetMonthlyBudget()
    {
        using var db = _factory.CreateDbContext();
        var setting = db.AppSettings.AsNoTracking().FirstOrDefault(x => x.Key == MonthlyBudgetKey);

        if (setting is not null
            && decimal.TryParse(setting.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var budget)
            && budget > 0m)
        {
            return budget;
        }

        return DefaultMonthlyBudget;
    }

    public OperationResult SetMonthlyBudget(decimal budget)
    {
        budget = decimal.Round(budget, 2);
        if (budget <= 0m || budget > AmountParser.MaxAmount)
        {
            return OperationResult.Fail("Bugetul lunar trebuie să fie pozitiv și cel mult 10.000.000 RON.");
        }

        var stored = budget.ToString(CultureInfo.InvariantCulture);

        using var db = _factory.CreateDbContext();
        var setting = db.AppSettings.FirstOrDefault(x => x.Key == MonthlyBudgetKey);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = MonthlyBudgetKey, Value = stored });
        }
        else
        {
            setting.Value = stored;
        }

        db.SaveChanges();
        return OperationResult.Ok();
    }
}
