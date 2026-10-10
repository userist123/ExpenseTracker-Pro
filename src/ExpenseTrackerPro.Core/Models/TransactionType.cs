namespace ExpenseTrackerPro.Core.Models;

/// <summary>
/// Kind of a transaction. The numeric values are never persisted: the database
/// stores the Romanian strings (see <c>TransactionTypeConverter</c>).
/// </summary>
public enum TransactionType
{
    Expense,
    Income,
}
