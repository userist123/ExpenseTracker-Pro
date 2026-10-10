namespace ExpenseTrackerPro.Core.Services;

/// <summary>Outcome of <see cref="TransactionService.ImportMany"/>.</summary>
public sealed record ImportReport(int Imported, int Duplicates, IReadOnlyList<ImportError> Errors);
