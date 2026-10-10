using ExpenseTrackerPro.Core.Data;
using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;
using ExpenseTrackerPro.ViewModels;

namespace ExpenseTrackerPro.Tests;

/// <summary>A real file database in a temp folder with all services, a fake dialog service and October 2026 as the month.</summary>
internal sealed class ViewModelFixture : IDisposable
{
    public static readonly DateTime Month = new(2026, 10, 1);

    private readonly TempFolder _folder = new();

    public ViewModelFixture()
    {
        DatabasePath = _folder.File("expense-tracker.db");
        Factory = AppDbContextFactory.ForFile(DatabasePath);
        using (var db = Factory.CreateDbContext())
        {
            DatabaseInitializer.Initialize(db);
        }

        Settings = new SettingsService(Factory);
        Transactions = new TransactionService(Factory, Settings);
        Backup = new BackupService(DatabasePath);
    }

    public string DatabasePath { get; }

    public AppDbContextFactory Factory { get; }

    public SettingsService Settings { get; }

    public TransactionService Transactions { get; }

    public BackupService Backup { get; }

    public FakeDialogService Dialogs { get; } = new();

    public string FilePath(string name) => _folder.File(name);

    /// <summary>Creates the view model (so seed data first) and shows October 2026.</summary>
    public MainViewModel CreateViewModel()
    {
        var viewModel = new MainViewModel(Transactions, Settings, Backup, Dialogs);
        viewModel.CurrentMonth = Month;
        return viewModel;
    }

    public Guid AddExpense(string description, decimal amount = 10m, string category = "Altele", int day = 5)
    {
        var result = Transactions.Add(TransactionType.Expense, category, description, amount, new DateTime(2026, 10, day));
        Assert.True(result.Success, result.Error);
        return result.Value!.Id;
    }

    public Guid AddIncome(string description, decimal amount = 100m, string category = "Salariu", int day = 1)
    {
        var result = Transactions.Add(TransactionType.Income, category, description, amount, new DateTime(2026, 10, day));
        Assert.True(result.Success, result.Error);
        return result.Value!.Id;
    }

    /// <summary>Inserts a row the way old versions could: bypassing the category validation.</summary>
    public Guid AddLegacy(TransactionType type, string category, string description, decimal amount)
    {
        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            Type = type,
            Category = category,
            Description = description,
            Amount = amount,
            Date = new DateTime(2026, 10, 5),
            CreatedAtUtc = DateTime.UtcNow,
        };

        using var db = Factory.CreateDbContext();
        db.Transactions.Add(item);
        db.SaveChanges();
        return item.Id;
    }

    public void Dispose()
    {
        _folder.Dispose();
    }
}
