using System.Globalization;
using System.Text;
using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.Core.Services;

public static class CsvExporter
{
    public const string Header = "Data;Descriere;Categorie;Tip;SumaRON";

    private const string LineBreak = "\r\n";
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>File name for a month export, e.g. <c>tranzactii-2026-10.csv</c>.</summary>
    public static string GetFileName(int year, int month)
    {
        return $"tranzactii-{year:0000}-{month:00}.csv";
    }

    /// <summary>Builds the CSV text (without BOM; the BOM is added when writing the file).</summary>
    public static string BuildCsv(IEnumerable<TransactionItem> items)
    {
        var csv = new StringBuilder(Header).Append(LineBreak);

        foreach (var item in items)
        {
            csv.Append(item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(';')
                .Append(Quote(item.Description)).Append(';')
                .Append(Quote(item.Category)).Append(';')
                .Append(Quote(item.Type.ToDisplayName())).Append(';')
                .Append(item.Amount.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(LineBreak);
        }

        return csv.ToString();
    }

    /// <summary>
    /// Writes the month export (UTF-8 with BOM) into <paramref name="folder"/>, overwriting a previous export of the
    /// same month, and returns the full path. May throw <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/>; callers are expected to handle both.
    /// </summary>
    public static string ExportMonth(IEnumerable<TransactionItem> items, string folder, int year, int month)
    {
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, GetFileName(year, month));
        File.WriteAllText(path, BuildCsv(items), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        return path;
    }

    /// <summary>Quotes a text field and neutralizes spreadsheet formula injection.</summary>
    public static string Quote(string value)
    {
        if (value.Length > 0 && Array.IndexOf(FormulaTriggers, value[0]) >= 0)
        {
            value = "'" + value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
