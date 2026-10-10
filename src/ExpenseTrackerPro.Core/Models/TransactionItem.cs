namespace ExpenseTrackerPro.Core.Models;

public sealed class TransactionItem
{
    public Guid Id { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Expense;

    public string Category { get; set; } = "Altele";

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
