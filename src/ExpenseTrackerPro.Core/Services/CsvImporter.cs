using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.Core.Services;

/// <summary>
/// Reads the files written by <see cref="CsvExporter"/>: header <see cref="CsvExporter.Header"/>, ';' separated,
/// quoted text fields with "" escaping, dates as yyyy-MM-dd and amounts with an invariant decimal point.
/// Only the format is checked here; categories, ranges and duplicates are handled by <see cref="TransactionService"/>.
/// </summary>
public static partial class CsvImporter
{
    public const long MaxFileSizeBytes = 5_000_000;

    private const int ColumnCount = 5;

    /// <summary>
    /// Reads a CSV file (UTF-8 with or without BOM). May throw <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/>; callers are expected to handle both.
    /// </summary>
    public static CsvParseResult ParseFile(string path)
    {
        if (new FileInfo(path).Length > MaxFileSizeBytes)
        {
            return Failed(1, "Fișierul este prea mare (maximum 5 MB).");
        }

        return Parse(File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)));
    }

    public static CsvParseResult Parse(string text)
    {
        var records = ReadRecords(text.TrimStart('﻿'));
        if (records.Count == 0)
        {
            return Failed(1, "Fișierul este gol.");
        }

        var header = records[0];
        if (header.Error is not null || string.Join(';', header.Fields).Trim() != CsvExporter.Header)
        {
            return Failed(header.LineNumber, $"Antet invalid. Se așteaptă: {CsvExporter.Header}");
        }

        var rows = new List<ImportRow>();
        var errors = new List<ImportError>();

        foreach (var record in records.Skip(1))
        {
            var error = record.Error;
            if (error is null && record.Fields.Count != ColumnCount)
            {
                error = $"Se așteptau {ColumnCount} coloane, dar au fost găsite {record.Fields.Count}.";
            }

            ImportRow? row = null;
            if (error is null)
            {
                row = ParseRow(record, out error);
            }

            if (row is not null)
            {
                rows.Add(row);
            }
            else
            {
                errors.Add(new ImportError(record.LineNumber, error ?? "Rând invalid."));
            }
        }

        return new CsvParseResult(rows, errors);
    }

    private static ImportRow? ParseRow(CsvRecord record, out string? error)
    {
        var fields = record.Fields;

        if (!DateTime.TryParseExact(
                fields[0].Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            error = "Data nu este validă (format așteptat: AAAA-LL-ZZ).";
            return null;
        }

        var typeText = RemoveFormulaGuard(fields[3]).Trim();
        TransactionType type;
        if (string.Equals(typeText, TransactionTypeExtensions.IncomeDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            type = TransactionType.Income;
        }
        else if (string.Equals(typeText, TransactionTypeExtensions.ExpenseDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            type = TransactionType.Expense;
        }
        else
        {
            error = $"Tipul „{typeText}” nu este valid (așteptat: Venit sau Cheltuială).";
            return null;
        }

        var amountText = fields[4].Trim();
        if (!AmountPattern().IsMatch(amountText)
            || !decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
        {
            error = "Suma nu este validă (format așteptat: 12.50, cu punct zecimal).";
            return null;
        }

        error = null;
        return new ImportRow(
            record.LineNumber,
            date,
            RemoveFormulaGuard(fields[1]),
            RemoveFormulaGuard(fields[2]).Trim(),
            type,
            amount);
    }

    /// <summary>Undoes the leading apostrophe that <see cref="CsvExporter.Quote"/> adds before formula characters.</summary>
    private static string RemoveFormulaGuard(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && Array.IndexOf(CsvExporter.FormulaTriggers, value[1]) >= 0)
        {
            return value[1..];
        }

        return value;
    }

    private static CsvParseResult Failed(int line, string message)
    {
        return new CsvParseResult([], [new ImportError(line, message)]);
    }

    /// <summary>
    /// Splits the text into records of ';' separated fields. Quoted fields may contain ';', line breaks and "" escapes.
    /// Blank lines are skipped. A record that cannot be read gets an <see cref="CsvRecord.Error"/>.
    /// </summary>
    private static List<CsvRecord> ReadRecords(string text)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var recordLine = 1;
        var inQuotes = false;
        var fieldWasQuoted = false;
        var closedQuote = false;
        string? error = null;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            fieldWasQuoted = false;
            closedQuote = false;
        }

        void EndRecord()
        {
            EndField();
            var isBlank = fields.Count == 1 && fields[0].Length == 0 && error is null;
            if (!isBlank)
            {
                records.Add(new CsvRecord(recordLine, fields, error));
            }

            fields = [];
            error = null;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                        closedQuote = true;
                    }
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }

                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0 && !fieldWasQuoted:
                    inQuotes = true;
                    fieldWasQuoted = true;
                    break;
                case '"':
                    error ??= "Ghilimele neașteptate în mijlocul unui câmp.";
                    field.Append(c);
                    break;
                case ';':
                    EndField();
                    break;
                case '\r':
                case '\n':
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    EndRecord();
                    line++;
                    recordLine = line;
                    break;
                default:
                    if (closedQuote)
                    {
                        error ??= "Text neașteptat după ghilimelele de închidere ale unui câmp.";
                    }

                    field.Append(c);
                    break;
            }
        }

        if (inQuotes)
        {
            fields.Add(field.ToString());
            records.Add(new CsvRecord(recordLine, fields, "Câmp între ghilimele neînchis."));
        }
        else if (field.Length > 0 || fields.Count > 0 || fieldWasQuoted)
        {
            EndRecord();
        }

        return records;
    }

    [GeneratedRegex("^[0-9]+(\\.[0-9]{1,2})?$")]
    private static partial Regex AmountPattern();

    private sealed record CsvRecord(int LineNumber, List<string> Fields, string? Error);
}
