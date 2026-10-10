using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.Tests;

internal static class TestItems
{
    public static TransactionItem Create(
        TransactionType type,
        string category,
        string description,
        decimal amount,
        DateTime? date = null)
    {
        return new TransactionItem
        {
            Id = Guid.NewGuid(),
            Type = type,
            Category = category,
            Description = description,
            Amount = amount,
            Date = date ?? new DateTime(2026, 10, 5),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public static TransactionItem Expense(string category, string description, decimal amount, DateTime? date = null)
    {
        return Create(TransactionType.Expense, category, description, amount, date);
    }

    public static TransactionItem Income(string category, string description, decimal amount, DateTime? date = null)
    {
        return Create(TransactionType.Income, category, description, amount, date);
    }
}
