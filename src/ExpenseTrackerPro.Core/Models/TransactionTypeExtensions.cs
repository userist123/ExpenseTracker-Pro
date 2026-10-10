namespace ExpenseTrackerPro.Core.Models;

public static class TransactionTypeExtensions
{
    public const string ExpenseDisplayName = "Cheltuială";
    public const string IncomeDisplayName = "Venit";

    /// <summary>Romanian label shown in the UI and written to CSV files.</summary>
    public static string ToDisplayName(this TransactionType type)
    {
        return type switch
        {
            TransactionType.Expense => ExpenseDisplayName,
            TransactionType.Income => IncomeDisplayName,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tip de tranzacție necunoscut."),
        };
    }
}
