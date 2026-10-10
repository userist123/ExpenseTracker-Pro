using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public sealed class TransactionServiceImportTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly TransactionService _service;

    public TransactionServiceImportTests()
    {
        _service = new TransactionService(_database.Factory, new SettingsService(_database.Factory));
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    [Fact]
    public void Inserts_valid_rows_and_normalizes_them()
    {
        var report = _service.ImportMany(
        [
            Row(2, "  Cafea  ", 12.504m),
            Row(3, "Prânz", 30m, date: new DateTime(2026, 10, 6, 14, 0, 0)),
        ]);

        Assert.Equal(2, report.Imported);
        Assert.Equal(0, report.Duplicates);
        Assert.Empty(report.Errors);

        var saved = _service.GetMonth(2026, 10);
        Assert.Equal(2, saved.Count);
        Assert.Contains(saved, x => x.Description == "Cafea" && x.Amount == 12.50m);
        Assert.Contains(saved, x => x.Description == "Prânz" && x.Date == new DateTime(2026, 10, 6));
        Assert.All(saved, x => Assert.NotEqual(Guid.Empty, x.Id));
    }

    [Fact]
    public void Reports_invalid_rows_with_their_line_numbers_and_imports_the_rest()
    {
        var report = _service.ImportMany(
        [
            Row(2, "Bun", 10m),
            Row(3, "", 10m),
            Row(4, "Sumă rea", 0m),
            Row(5, "Categorie rea", 10m, category: "Salariu"),
            Row(6, "Alt bun", 20m),
        ]);

        Assert.Equal(2, report.Imported);
        Assert.Equal([3, 4, 5], report.Errors.Select(x => x.LineNumber));
        Assert.Equal(["Bun", "Alt bun"], _service.GetMonth(2026, 10).Select(x => x.Description).OrderByDescending(x => x));
    }

    [Fact]
    public void Skips_rows_that_already_exist()
    {
        _service.Add(TransactionType.Expense, "Alimentație", "Cafea", 12m, new DateTime(2026, 10, 5));

        var report = _service.ImportMany([Row(2, "Cafea", 12m), Row(3, "Ceai", 8m)]);

        Assert.Equal(1, report.Imported);
        Assert.Equal(1, report.Duplicates);
        Assert.Equal(2, _service.GetMonth(2026, 10).Count);
    }

    [Theory]
    [InlineData("Cafea", 12.01, "Alimentație", TransactionType.Expense, 5)]
    [InlineData("cafea", 12, "Alimentație", TransactionType.Expense, 5)]
    [InlineData("Cafea", 12, "Altele", TransactionType.Expense, 5)]
    [InlineData("Cafea", 12, "Altele", TransactionType.Income, 5)]
    [InlineData("Cafea", 12, "Alimentație", TransactionType.Expense, 6)]
    public void Rows_that_differ_in_any_field_are_not_duplicates(
        string description,
        double amount,
        string category,
        TransactionType type,
        int day)
    {
        _service.Add(TransactionType.Expense, "Alimentație", "Cafea", 12m, new DateTime(2026, 10, 5));

        var report = _service.ImportMany(
            [Row(2, description, (decimal)amount, category: category, type: type, date: new DateTime(2026, 10, day))]);

        Assert.Equal(0, report.Duplicates);
        Assert.Equal(1, report.Imported);
    }

    [Fact]
    public void Surrounding_whitespace_in_the_description_does_not_defeat_duplicate_detection()
    {
        _service.Add(TransactionType.Expense, "Alimentație", "Cafea", 12m, new DateTime(2026, 10, 5));

        var report = _service.ImportMany([Row(2, "  Cafea ", 12m)]);

        Assert.Equal(0, report.Imported);
        Assert.Equal(1, report.Duplicates);
    }

    [Fact]
    public void Importing_the_same_rows_twice_adds_nothing_the_second_time()
    {
        ImportRow[] rows =
        [
            Row(2, "Cafea", 12m),
            Row(3, "Cafea", 12m),
            Row(4, "Ceai", 8m),
        ];

        var first = _service.ImportMany(rows);
        var second = _service.ImportMany(rows);

        // Two equal purchases inside one file are both real transactions.
        Assert.Equal(3, first.Imported);
        Assert.Equal(0, first.Duplicates);
        Assert.Equal(0, second.Imported);
        Assert.Equal(3, second.Duplicates);
        Assert.Equal(3, _service.GetMonth(2026, 10).Count);
    }

    [Fact]
    public void Only_the_missing_copies_of_a_repeated_row_are_imported()
    {
        _service.Add(TransactionType.Expense, "Alimentație", "Cafea", 12m, new DateTime(2026, 10, 5));

        var report = _service.ImportMany([Row(2, "Cafea", 12m), Row(3, "Cafea", 12m)]);

        Assert.Equal(1, report.Imported);
        Assert.Equal(1, report.Duplicates);
        Assert.Equal(2, _service.GetMonth(2026, 10).Count);
    }

    [Fact]
    public void Rows_across_several_months_are_all_imported()
    {
        var report = _service.ImportMany(
        [
            Row(2, "Septembrie", 5m, date: new DateTime(2026, 9, 30)),
            Row(3, "Octombrie", 5m, date: new DateTime(2026, 10, 1)),
            Row(4, "Noiembrie", 5m, date: new DateTime(2026, 11, 15)),
        ]);

        Assert.Equal(3, report.Imported);
        Assert.Single(_service.GetMonth(2026, 9));
        Assert.Single(_service.GetMonth(2026, 10));
        Assert.Single(_service.GetMonth(2026, 11));
    }

    [Fact]
    public void An_empty_import_does_nothing()
    {
        var report = _service.ImportMany([]);

        Assert.Equal(0, report.Imported);
        Assert.Equal(0, report.Duplicates);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public void A_parsed_file_can_be_imported_end_to_end()
    {
        var csv = CsvExporter.BuildCsv(
        [
            TestItems.Income("Salariu", "Salariu", 8000m, new DateTime(2026, 10, 1)),
            TestItems.Expense("Utilități", "Chirie", 2000m, new DateTime(2026, 10, 3)),
        ]);

        var parsed = CsvImporter.Parse(csv);
        var report = _service.ImportMany(parsed.Rows);

        Assert.Equal(2, report.Imported);
        Assert.Equal(6000m, _service.GetSummary(2026, 10).Balance);
    }

    private static ImportRow Row(
        int line,
        string description,
        decimal amount,
        string category = "Alimentație",
        TransactionType type = TransactionType.Expense,
        DateTime? date = null)
    {
        return new ImportRow(line, date ?? new DateTime(2026, 10, 5), description, category, type, amount);
    }
}
