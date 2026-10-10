using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.Core.Services;

public static class Categories
{
    public static IReadOnlyList<string> Income { get; } =
    [
        "Salariu",
        "Freelance",
        "Investiții",
        "Cadouri",
        "Altele",
    ];

    public static IReadOnlyList<string> Expense { get; } =
    [
        "Alimentație",
        "Utilități",
        "Transport",
        "Sănătate",
        "Distracție",
        "Investiții",
        "Altele",
    ];

    public static IReadOnlyList<string> For(TransactionType type)
    {
        return type switch
        {
            TransactionType.Income => Income,
            TransactionType.Expense => Expense,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tip de tranzacție necunoscut."),
        };
    }

    public static bool IsValid(TransactionType type, string? category)
    {
        return category is not null && For(type).Contains(category, StringComparer.Ordinal);
    }

    /// <summary>First category of the given type, used as the default selection.</summary>
    public static string Default(TransactionType type) => For(type)[0];
}
