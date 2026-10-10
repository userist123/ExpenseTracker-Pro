namespace ExpenseTrackerPro.Core.Services;

/// <summary>Total spent in one category and its share (0-1) of all expenses.</summary>
public sealed record CategoryExpense(string Category, decimal Amount, double Share);
