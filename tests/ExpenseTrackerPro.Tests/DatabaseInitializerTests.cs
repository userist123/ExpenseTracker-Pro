using ExpenseTrackerPro.Core.Data;
using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerPro.Tests;

public class DatabaseInitializerTests
{
    // Exactly what the old EnsureCreated() call produced for the original model.
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

    [Fact]
    public void Fresh_database_gets_all_tables_and_all_migrations()
    {
        using var database = new TestDatabase(initialize: false);
        using var db = database.Factory.CreateDbContext();

        DatabaseInitializer.Initialize(db);

        Assert.True(TableExists(database.Connection, "Transactions"));
        Assert.True(TableExists(database.Connection, "AppSettings"));
        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.Equal(2, db.Database.GetAppliedMigrations().Count());
    }

    [Fact]
    public void Legacy_database_created_by_EnsureCreated_is_baselined_and_keeps_its_data()
    {
        using var database = new TestDatabase(initialize: false);
        Execute(database.Connection, LegacySchema);
        Execute(
            database.Connection,
            """
            INSERT INTO "Transactions" ("Id", "Type", "Category", "Description", "Amount", "Date", "CreatedAtUtc")
            VALUES ('22222222-2222-2222-2222-222222222222', 'Cheltuială', 'Alimentație', 'Date vechi', '420.5', '2026-10-10 00:00:00', '2026-10-10 08:00:00');
            """);
        Assert.False(TableExists(database.Connection, "__EFMigrationsHistory"));

        using (var db = database.Factory.CreateDbContext())
        {
            DatabaseInitializer.Initialize(db);
        }

        using var verify = database.Factory.CreateDbContext();
        var item = Assert.Single(verify.Transactions.AsNoTracking().ToList());
        Assert.Equal(TransactionType.Expense, item.Type);
        Assert.Equal("Date vechi", item.Description);
        Assert.Equal(420.5m, item.Amount);

        Assert.True(TableExists(database.Connection, "AppSettings"));
        Assert.Empty(verify.Database.GetPendingMigrations());

        var applied = verify.Database.GetAppliedMigrations().ToList();
        Assert.Equal(2, applied.Count);
        Assert.EndsWith("_InitialCreate", applied[0]);
        Assert.EndsWith("_AddSettings", applied[1]);

        // The settings table is usable right away.
        var settings = new SettingsService(database.Factory);
        Assert.True(settings.SetMonthlyBudget(5000m).Success);
        Assert.Equal(5000m, settings.GetMonthlyBudget());
    }

    [Fact]
    public void Initialize_is_idempotent()
    {
        using var database = new TestDatabase(initialize: false);
        Execute(database.Connection, LegacySchema);

        for (var i = 0; i < 3; i++)
        {
            using var db = database.Factory.CreateDbContext();
            DatabaseInitializer.Initialize(db);
        }

        using var verify = database.Factory.CreateDbContext();
        Assert.Equal(2, verify.Database.GetAppliedMigrations().Count());
    }

    [Fact]
    public void Baseline_records_the_ef_product_version()
    {
        using var database = new TestDatabase(initialize: false);
        Execute(database.Connection, LegacySchema);
        using var db = database.Factory.CreateDbContext();

        DatabaseInitializer.Initialize(db);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" LIMIT 1;";
        var version = (string?)command.ExecuteScalar();
        Assert.Matches(@"^8\.0\.\d+$", version);
    }

    private static bool TableExists(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", name);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
