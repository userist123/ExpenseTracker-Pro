using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;
using Microsoft.Data.Sqlite;

namespace ExpenseTrackerPro.Tests;

public class TransactionTypeTests
{
    [Fact]
    public void Display_names_are_romanian()
    {
        Assert.Equal("Cheltuială", TransactionType.Expense.ToDisplayName());
        Assert.Equal("Venit", TransactionType.Income.ToDisplayName());
    }

    [Fact]
    public void Converter_stores_the_existing_romanian_strings()
    {
        using var database = new TestDatabase();
        var settings = new SettingsService(database.Factory);
        var service = new TransactionService(database.Factory, settings);

        service.Add(TransactionType.Income, "Salariu", "Salariu octombrie", 5000m, new DateTime(2026, 10, 5));
        service.Add(TransactionType.Expense, "Transport", "Abonament", 80m, new DateTime(2026, 10, 6));

        var stored = ReadStoredTypes(database.Connection);

        Assert.Equal(["Cheltuială", "Venit"], stored);
    }

    [Fact]
    public void Converter_reads_rows_written_by_the_old_version()
    {
        using var database = new TestDatabase();
        using (var command = database.Connection.CreateCommand())
        {
            command.CommandText =
                "INSERT INTO \"Transactions\" (\"Id\", \"Type\", \"Category\", \"Description\", \"Amount\", \"Date\", \"CreatedAtUtc\") " +
                "VALUES ('11111111-1111-1111-1111-111111111111', 'Venit', 'Salariu', 'Vechi', '100.0', '2026-10-05 00:00:00', '2026-10-05 08:00:00');";
            command.ExecuteNonQuery();
        }

        var settings = new SettingsService(database.Factory);
        var items = new TransactionService(database.Factory, settings).GetMonth(2026, 10);

        var item = Assert.Single(items);
        Assert.Equal(TransactionType.Income, item.Type);
    }

    private static List<string> ReadStoredTypes(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT \"Type\" FROM \"Transactions\" ORDER BY \"Type\";";

        var result = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}
