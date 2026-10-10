using System.Text;
using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public sealed class CsvExporterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "etp-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void BuildCsv_writes_header_and_quoted_rows()
    {
        var csv = CsvExporter.BuildCsv([Item(TransactionType.Income, "Salariu", "Salariu lunar", 8600m, new DateTime(2026, 10, 5))]);

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Data;Descriere;Categorie;Tip;SumaRON", lines[0]);
        Assert.Equal("2026-10-05;\"Salariu lunar\";\"Salariu\";\"Venit\";8600.00", lines[1]);
    }

    [Fact]
    public void BuildCsv_writes_the_expense_label_and_invariant_decimals()
    {
        var csv = CsvExporter.BuildCsv([Item(TransactionType.Expense, "Altele", "Test", 12.5m, new DateTime(2026, 1, 2))]);

        Assert.Contains("2026-01-02;\"Test\";\"Altele\";\"Cheltuială\";12.50", csv);
    }

    [Fact]
    public void Quote_doubles_embedded_quotes()
    {
        Assert.Equal("\"Spune \"\"salut\"\"\"", CsvExporter.Quote("Spune \"salut\""));
    }

    [Theory]
    [InlineData("=SUM(A1:A9)")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@cmd")]
    [InlineData("\tTab")]
    [InlineData("\rReturn")]
    public void Quote_neutralizes_formula_injection(string value)
    {
        var quoted = CsvExporter.Quote(value);

        Assert.StartsWith("\"'", quoted);
    }

    [Fact]
    public void Quote_leaves_ordinary_text_untouched()
    {
        Assert.Equal("\"Cumpărături = ok\"", CsvExporter.Quote("Cumpărături = ok"));
    }

    [Fact]
    public void ExportMonth_writes_a_utf8_file_with_bom_named_after_the_month()
    {
        var path = CsvExporter.ExportMonth(
            [Item(TransactionType.Expense, "Sănătate", "Medicamente ăâîșț", 55m, new DateTime(2026, 10, 3))],
            _folder,
            2026,
            10);

        Assert.Equal(Path.Combine(_folder, "tranzactii-2026-10.csv"), path);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Contains("Medicamente ăâîșț", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void ExportMonth_overwrites_a_previous_export_of_the_same_month()
    {
        CsvExporter.ExportMonth([Item(TransactionType.Expense, "Altele", "Prima", 1m, new DateTime(2026, 10, 3))], _folder, 2026, 10);
        var path = CsvExporter.ExportMonth([Item(TransactionType.Expense, "Altele", "A doua", 2m, new DateTime(2026, 10, 3))], _folder, 2026, 10);

        var text = File.ReadAllText(path);
        Assert.Contains("A doua", text);
        Assert.DoesNotContain("Prima", text);
    }

    [Fact]
    public void GetFileName_pads_the_month()
    {
        Assert.Equal("tranzactii-2026-03.csv", CsvExporter.GetFileName(2026, 3));
    }

    private static TransactionItem Item(TransactionType type, string category, string description, decimal amount, DateTime date)
    {
        return new TransactionItem
        {
            Id = Guid.NewGuid(),
            Type = type,
            Category = category,
            Description = description,
            Amount = amount,
            Date = date,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
