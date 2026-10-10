using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.Core.Services;

/// <summary>A transaction read from an import file; <see cref="LineNumber"/> points back to the source line.</summary>
public sealed record ImportRow(
    int LineNumber,
    DateTime Date,
    string Description,
    string Category,
    TransactionType Type,
    decimal Amount);
