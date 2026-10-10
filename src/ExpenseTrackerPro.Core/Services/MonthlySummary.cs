namespace ExpenseTrackerPro.Core.Services;

public sealed record MonthlySummary(
    decimal Income,
    decimal Expenses,
    decimal Balance,
    decimal Budget,
    decimal RemainingBudget);
