namespace ExpenseTrackerPro.Core.Services;

public sealed record CsvParseResult(IReadOnlyList<ImportRow> Rows, IReadOnlyList<ImportError> Errors);
