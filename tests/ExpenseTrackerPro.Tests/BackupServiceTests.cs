using ExpenseTrackerPro.Core.Data;
using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerPro.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private const string LegacySchema = """
        CREATE TABLE "Transactions" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Transactions" PRIMARY KEY,
            "Type" TEXT NOT NULL,
            "Category" TEXT NOT NULL,
            "Description" TEXT NOT NULL,
            "Amount" TEXT NOT NULL,
            "Date" TEXT NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL
        );
        CREATE INDEX "IX_Transactions_Date" ON "Transactions" ("Date");
        """;

    private readonly TempFolder _folder = new();
    private readonly string _livePath;
    private readonly AppDbContextFactory _factory;
    private readonly TransactionService _transactions;
    private readonly BackupService _backup;

    public BackupServiceTests()
    {
        _livePath = _folder.File("expense-tracker.db");
        _factory = AppDbContextFactory.ForFile(_livePath);
        using (var db = _factory.CreateDbContext())
        {
            DatabaseInitializer.Initialize(db);
        }

        _transactions = new TransactionService(_factory, new SettingsService(_factory));
        _backup = new BackupService(_livePath);
    }

    public void Dispose()
    {
        _folder.Dispose();
    }

    [Fact]
    public void Backup_creates_a_copy_with_the_current_data()
    {
        AddExpense("Cafea");
        var destination = _folder.File("copie.db");

        var result = _backup.Backup(destination);

        Assert.True(result.Success, result.Error);
        Assert.Equal(["Cafea"], DescriptionsIn(destination));
    }

    [Fact]
    public void Backup_works_while_the_app_has_the_database_open()
    {
        AddExpense("Prima");
        using var db = _factory.CreateDbContext();
        Assert.Single(db.Transactions.AsNoTracking().ToList());

        var result = _backup.Backup(_folder.File("deschisa.db"));

        Assert.True(result.Success, result.Error);
        AddExpense("A doua");
        Assert.Equal(["Prima"], DescriptionsIn(_folder.File("deschisa.db")));
    }

    [Fact]
    public void Backup_replaces_an_existing_backup_file()
    {
        var destination = _folder.File("copie.db");
        AddExpense("Vechi");
        Assert.True(_backup.Backup(destination).Success);
        AddExpense("Nou");

        var result = _backup.Backup(destination);

        Assert.True(result.Success, result.Error);
        Assert.Equal(["Nou", "Vechi"], DescriptionsIn(destination).OrderBy(x => x));
        Assert.Empty(Directory.GetFiles(_folder.Path, "*.tmp"));
    }

    [Fact]
    public void Backup_creates_the_destination_folder()
    {
        var destination = Path.Combine(_folder.Path, "sub", "dosar", "copie.db");

        var result = _backup.Backup(destination);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(destination));
    }

    [Fact]
    public void Backup_refuses_to_overwrite_the_live_database()
    {
        AddExpense("Cafea");

        var result = _backup.Backup(_livePath);

        Assert.False(result.Success);
        Assert.Equal(["Cafea"], DescriptionsIn(_livePath));
    }

    [Fact]
    public void Restore_replaces_the_current_data_and_keeps_a_before_restore_copy()
    {
        AddExpense("Din backup");
        var backupPath = _folder.File("backup.db");
        Assert.True(_backup.Backup(backupPath).Success);
        AddExpense("După backup");

        var result = _backup.Restore(backupPath);

        Assert.True(result.Success, result.Error);
        Assert.Equal(["Din backup"], _transactions.GetMonth(2026, 10).Select(x => x.Description));
        Assert.Equal(_folder.File(BackupService.BeforeRestoreFileName), _backup.BeforeRestorePath);
        Assert.Equal(["Din backup", "După backup"], DescriptionsIn(_backup.BeforeRestorePath).OrderBy(x => x));
    }

    [Fact]
    public void Restore_keeps_the_app_usable_afterwards()
    {
        AddExpense("Unu");
        var backupPath = _folder.File("backup.db");
        _backup.Backup(backupPath);

        Assert.True(_backup.Restore(backupPath).Success);
        AddExpense("Doi");

        Assert.Equal(2, _transactions.GetMonth(2026, 10).Count);
    }

    [Fact]
    public void Restore_brings_back_the_budget_setting()
    {
        var settings = new SettingsService(_factory);
        settings.SetMonthlyBudget(1234m);
        var backupPath = _folder.File("backup.db");
        _backup.Backup(backupPath);
        settings.SetMonthlyBudget(9999m);

        Assert.True(_backup.Restore(backupPath).Success);

        Assert.Equal(1234m, settings.GetMonthlyBudget());
    }

    [Fact]
    public void Restore_rejects_a_file_that_is_not_a_database_and_leaves_the_data_untouched()
    {
        AddExpense("Rămâne");
        var bogus = _folder.File("text.db");
        File.WriteAllText(bogus, "Acesta nu este un fișier SQLite, ci doar text oarecare.");

        var result = _backup.Restore(bogus);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(["Rămâne"], _transactions.GetMonth(2026, 10).Select(x => x.Description));
        Assert.False(File.Exists(_backup.BeforeRestorePath));
    }

    [Fact]
    public void Restore_rejects_a_database_without_a_transactions_table()
    {
        AddExpense("Rămâne");
        var other = _folder.File("altceva.db");
        Execute(other, "CREATE TABLE \"Altceva\" (\"Id\" INTEGER PRIMARY KEY);");

        var result = _backup.Restore(other);

        Assert.False(result.Success);
        Assert.Equal(["Rămâne"], _transactions.GetMonth(2026, 10).Select(x => x.Description));
        Assert.False(File.Exists(_backup.BeforeRestorePath));
    }

    [Fact]
    public void Restore_rejects_an_empty_file()
    {
        AddExpense("Rămâne");
        var empty = _folder.File("gol.db");
        File.WriteAllBytes(empty, []);

        var result = _backup.Restore(empty);

        Assert.False(result.Success);
        Assert.Equal(["Rămâne"], _transactions.GetMonth(2026, 10).Select(x => x.Description));
    }

    [Fact]
    public void Restore_rejects_a_missing_file()
    {
        var result = _backup.Restore(_folder.File("nu-exista.db"));

        Assert.False(result.Success);
    }

    [Fact]
    public void Restore_refuses_the_live_database_itself()
    {
        AddExpense("Rămâne");

        var result = _backup.Restore(_livePath);

        Assert.False(result.Success);
        Assert.Equal(["Rămâne"], _transactions.GetMonth(2026, 10).Select(x => x.Description));
    }

    [Fact]
    public void Restore_migrates_a_legacy_database_without_migration_history()
    {
        AddExpense("Se pierde");
        var legacy = _folder.File("vechi.db");
        Execute(legacy, LegacySchema);
        Execute(
            legacy,
            """
            INSERT INTO "Transactions" ("Id", "Type", "Category", "Description", "Amount", "Date", "CreatedAtUtc")
            VALUES ('33333333-3333-3333-3333-333333333333', 'Cheltuială', 'Alimentație', 'Date vechi', '420.5', '2026-10-10 00:00:00', '2026-10-10 08:00:00');
            """);

        var result = _backup.Restore(legacy);

        Assert.True(result.Success, result.Error);
        var item = Assert.Single(_transactions.GetMonth(2026, 10));
        Assert.Equal("Date vechi", item.Description);
        Assert.Equal(420.5m, item.Amount);

        using var db = _factory.CreateDbContext();
        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.Equal(2, db.Database.GetAppliedMigrations().Count());

        // The settings table added by the later migration exists and works.
        var settings = new SettingsService(_factory);
        Assert.True(settings.SetMonthlyBudget(5000m).Success);
        Assert.Equal(5000m, settings.GetMonthlyBudget());
    }

    [Fact]
    public void A_backup_taken_from_a_restored_database_restores_again()
    {
        AddExpense("Unu");
        var first = _folder.File("unu.db");
        _backup.Backup(first);
        _backup.Restore(first);
        var second = _folder.File("doi.db");

        Assert.True(_backup.Backup(second).Success);
        AddExpense("Doi");
        Assert.True(_backup.Restore(second).Success);

        Assert.Equal(["Unu"], _transactions.GetMonth(2026, 10).Select(x => x.Description));
    }

    private void AddExpense(string description)
    {
        var result = _transactions.Add(TransactionType.Expense, "Altele", description, 10m, new DateTime(2026, 10, 5));
        Assert.True(result.Success, result.Error);
    }

    private static List<string> DescriptionsIn(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Description\" FROM \"Transactions\" ORDER BY \"CreatedAtUtc\";";

        var result = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static void Execute(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
