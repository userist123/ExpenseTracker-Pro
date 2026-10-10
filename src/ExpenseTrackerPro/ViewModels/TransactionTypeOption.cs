using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.ViewModels;

/// <summary>An entry of the "Tip" combo box: the enum value and its Romanian label.</summary>
public sealed record TransactionTypeOption(TransactionType Value, string DisplayName);
