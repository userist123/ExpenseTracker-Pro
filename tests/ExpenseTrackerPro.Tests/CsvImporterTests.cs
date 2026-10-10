using System.Text;
using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public class CsvImporterTests
{
    private const string Header = "Data;Descriere;Categorie;Tip;SumaRON";

    private static string Csv(params string[] lines) => string.Join("\r\n", [Header, .. lines]) + "\r\n";

    [Fact]
    public void Parses_a_valid_file()
    {
        var result = CsvImporter.Parse(Csv(
            "2026-10-05;\"Salariu lunar\";\"Salariu\";\"Venit\";8600.00",
            "2026-10-07;\"Chirie\";\"Utilități\";\"Cheltuială\";2350.50"));

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);

        var income = result.Rows[0];
        Assert.Equal(2, income.LineNumber);
        Assert.Equal(new DateTime(2026, 10, 5), income.Date);
        Assert.Equal("Salariu lunar", income.Description);
        Assert.Equal("Salariu", income.Category);
        Assert.Equal(TransactionType.Income, income.Type);
        Assert.Equal(8600m, income.Amount);

        var expense = result.Rows[1];
        Assert.Equal(3, expense.LineNumber);
        Assert.Equal(TransactionType.Expense, expense.Type);
        Assert.Equal(2350.50m, expense.Amount);
    }

    [Fact]
    public void Accepts_unquoted_text_fields_and_lf_line_endings()
    {
        var text = Header + "\n2026-10-05;Cafea;Alimentație;Cheltuială;12.5\n";

        var result = CsvImporter.Parse(text);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Cafea", row.Description);
        Assert.Equal(12.5m, row.Amount);
    }

    [Fact]
    public void Ignores_a_leading_bom_character_and_blank_lines()
    {
        var text = "﻿" + Header + "\r\n\r\n2026-10-05;\"A\";\"Altele\";\"Cheltuială\";1.00\r\n\r\n";

        var result = CsvImporter.Parse(text);

        Assert.Empty(result.Errors);
        Assert.Equal(3, Assert.Single(result.Rows).LineNumber);
    }

    [Fact]
    public void ParseFile_reads_utf8_with_and_without_bom()
    {
        using var folder = new TempFolder();
        var content = Csv("2026-10-05;\"Medicamente ăâîșț\";\"Sănătate\";\"Cheltuială\";55.00");
        File.WriteAllText(folder.File("bom.csv"), content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllText(folder.File("nobom.csv"), content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        foreach (var name in new[] { "bom.csv", "nobom.csv" })
        {
            var result = CsvImporter.ParseFile(folder.File(name));

            Assert.Empty(result.Errors);
            var row = Assert.Single(result.Rows);
            Assert.Equal("Medicamente ăâîșț", row.Description);
            Assert.Equal("Sănătate", row.Category);
        }
    }

    [Fact]
    public void Handles_semicolons_and_escaped_quotes_inside_quoted_fields()
    {
        var result = CsvImporter.Parse(Csv(
            "2026-10-05;\"Pâine; lapte și \"\"ouă\"\"\";\"Alimentație\";\"Cheltuială\";20.00"));

        Assert.Empty(result.Errors);
        Assert.Equal("Pâine; lapte și \"ouă\"", Assert.Single(result.Rows).Description);
    }

    [Fact]
    public void Handles_line_breaks_inside_quoted_fields_and_keeps_line_numbers_physical()
    {
        var result = CsvImporter.Parse(Csv(
            "2026-10-05;\"Prima\r\nlinie\";\"Altele\";\"Cheltuială\";5.00",
            "2026-13-45;\"Rea\";\"Altele\";\"Cheltuială\";5.00"));

        var row = Assert.Single(result.Rows);
        Assert.Equal("Prima\r\nlinie", row.Description);
        var error = Assert.Single(result.Errors);
        Assert.Equal(4, error.LineNumber);
    }

    [Theory]
    [InlineData("'=SUM(A1)", "=SUM(A1)")]
    [InlineData("'+1", "+1")]
    [InlineData("'-2", "-2")]
    [InlineData("'@cmd", "@cmd")]
    [InlineData("'\tTab", "\tTab")]
    [InlineData("'\rReturn", "\rReturn")]
    public void Strips_the_formula_guard_apostrophe(string stored, string expected)
    {
        var result = CsvImporter.Parse(Csv($"2026-10-05;\"{stored}\";\"Altele\";\"Cheltuială\";1.00"));

        Assert.Empty(result.Errors);
        Assert.Equal(expected, Assert.Single(result.Rows).Description);
    }

    [Theory]
    [InlineData("'abc")]
    [InlineData("'")]
    [InlineData("O'Brien")]
    public void Keeps_apostrophes_that_are_not_a_formula_guard(string description)
    {
        var result = CsvImporter.Parse(Csv($"2026-10-05;\"{description}\";\"Altele\";\"Cheltuială\";1.00"));

        Assert.Equal(description, Assert.Single(result.Rows).Description);
    }

    [Theory]
    [InlineData("2026-13-01")]
    [InlineData("05.10.2026")]
    [InlineData("2026-10-5")]
    [InlineData("azi")]
    [InlineData("")]
    public void Reports_a_bad_date_with_the_line_number(string date)
    {
        var result = CsvImporter.Parse(Csv(
            "2026-10-05;\"Ok\";\"Altele\";\"Cheltuială\";1.00",
            $"{date};\"Rău\";\"Altele\";\"Cheltuială\";1.00"));

        Assert.Single(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Contains("Data", error.Message);
    }

    [Theory]
    [InlineData("12,50")]
    [InlineData("1.234,56")]
    [InlineData("-5.00")]
    [InlineData("abc")]
    [InlineData("12.345")]
    [InlineData("")]
    public void Reports_a_bad_amount_with_the_line_number(string amount)
    {
        var result = CsvImporter.Parse(Csv($"2026-10-05;\"Rău\";\"Altele\";\"Cheltuială\";{amount}"));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("Suma", error.Message);
    }

    [Fact]
    public void Reports_an_unknown_type()
    {
        var result = CsvImporter.Parse(Csv("2026-10-05;\"Rău\";\"Altele\";\"Transfer\";1.00"));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("Tipul", error.Message);
    }

    [Fact]
    public void Reports_the_wrong_number_of_columns()
    {
        var result = CsvImporter.Parse(Csv("2026-10-05;\"Rău\";\"Altele\";1.00"));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("coloane", error.Message);
    }

    [Fact]
    public void Reports_malformed_quotes()
    {
        var result = CsvImporter.Parse(Csv(
            "2026-10-05;\"Fără final;\"Altele\";\"Cheltuială\";1.00"));

        Assert.Empty(result.Rows);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error => Assert.Equal(2, error.LineNumber));
    }

    [Fact]
    public void Collects_all_errors_and_still_returns_the_valid_rows()
    {
        var result = CsvImporter.Parse(Csv(
            "2026-10-05;\"Bun\";\"Altele\";\"Cheltuială\";1.00",
            "2026-10-06;\"Tip rău\";\"Altele\";\"X\";1.00",
            "ieri;\"Dată rea\";\"Altele\";\"Cheltuială\";1.00",
            "2026-10-08;\"Bun 2\";\"Altele\";\"Venit\";2.00"));

        Assert.Equal(["Bun", "Bun 2"], result.Rows.Select(x => x.Description));
        Assert.Equal([3, 4], result.Errors.Select(x => x.LineNumber));
    }

    [Theory]
    [InlineData("Data,Descriere,Categorie,Tip,SumaRON")]
    [InlineData("Date;Description;Category;Type;Amount")]
    [InlineData("2026-10-05;\"A\";\"Altele\";\"Cheltuială\";1.00")]
    public void Rejects_a_wrong_header_on_line_one(string firstLine)
    {
        var result = CsvImporter.Parse(firstLine + "\r\n2026-10-05;\"A\";\"Altele\";\"Cheltuială\";1.00\r\n");

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
        Assert.Contains("Antet", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n\r\n")]
    public void Rejects_an_empty_file(string text)
    {
        var result = CsvImporter.Parse(text);

        Assert.Empty(result.Rows);
        Assert.Equal(1, Assert.Single(result.Errors).LineNumber);
    }

    [Fact]
    public void A_file_with_only_a_header_has_no_rows_and_no_errors()
    {
        var result = CsvImporter.Parse(Header + "\r\n");

        Assert.Empty(result.Rows);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ImportError_formats_with_the_line_number()
    {
        Assert.Equal("Linia 7: Ceva nu e bine.", new ImportError(7, "Ceva nu e bine.").ToString());
    }

    [Fact]
    public void Export_then_import_roundtrips_every_field()
    {
        var original = new[]
        {
            TestItems.Income("Salariu", "Salariu; \"octombrie\"", 8600m, new DateTime(2026, 10, 5)),
            TestItems.Expense("Alimentație", "Cumpărături ăâîșț", 420.5m, new DateTime(2026, 10, 7)),
            TestItems.Expense("Altele", "=HYPERLINK(\"x\")", 1m, new DateTime(2026, 10, 8)),
            TestItems.Expense("Altele", "+40 700 000 000", 99.99m, new DateTime(2026, 10, 9)),
            TestItems.Expense("Altele", "'deja cu apostrof", 3m, new DateTime(2026, 10, 10)),
            TestItems.Expense("Altele", "Linia unu\r\nlinia doi", 4m, new DateTime(2026, 10, 11)),
        };

        var result = CsvImporter.Parse(CsvExporter.BuildCsv(original));

        Assert.Empty(result.Errors);
        Assert.Equal(original.Length, result.Rows.Count);
        for (var i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i].Date, result.Rows[i].Date);
            Assert.Equal(original[i].Description, result.Rows[i].Description);
            Assert.Equal(original[i].Category, result.Rows[i].Category);
            Assert.Equal(original[i].Type, result.Rows[i].Type);
            Assert.Equal(original[i].Amount, result.Rows[i].Amount);
        }
    }

    [Fact]
    public void Exported_file_on_disk_roundtrips_through_ParseFile()
    {
        using var folder = new TempFolder();
        var path = CsvExporter.ExportMonth(
            [TestItems.Expense("Sănătate", "Medicamente", 55m, new DateTime(2026, 10, 3))],
            folder.Path,
            2026,
            10);

        var result = CsvImporter.ParseFile(path);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Medicamente", row.Description);
        Assert.Equal(55m, row.Amount);
    }

    [Fact]
    public void ParseFile_rejects_files_over_the_size_limit()
    {
        using var folder = new TempFolder();
        var path = folder.File("mare.csv");
        using (var stream = File.Create(path))
        {
            stream.SetLength(CsvImporter.MaxFileSizeBytes + 1);
        }

        var result = CsvImporter.ParseFile(path);

        Assert.Empty(result.Rows);
        Assert.Contains("mare", Assert.Single(result.Errors).Message);
    }
}
