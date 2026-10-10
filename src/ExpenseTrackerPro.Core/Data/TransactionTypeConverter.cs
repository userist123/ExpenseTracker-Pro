using ExpenseTrackerPro.Core.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExpenseTrackerPro.Core.Data;

/// <summary>
/// Maps <see cref="TransactionType"/> to the strings that already exist in users' databases
/// ("Venit" / "Cheltuială"), so no data migration is required.
/// </summary>
public sealed class TransactionTypeConverter : ValueConverter<TransactionType, string>
{
    public const string StoredIncome = "Venit";
    public const string StoredExpense = "Cheltuială";

    public TransactionTypeConverter()
        : base(type => ToStored(type), stored => FromStored(stored))
    {
    }

    private static string ToStored(TransactionType type)
    {
        return type switch
        {
            TransactionType.Income => StoredIncome,
            TransactionType.Expense => StoredExpense,
            _ => throw new InvalidOperationException($"Tip de tranzacție necunoscut: {type}."),
        };
    }

    private static TransactionType FromStored(string stored)
    {
        return stored switch
        {
            StoredIncome => TransactionType.Income,
            StoredExpense => TransactionType.Expense,
            _ => throw new InvalidOperationException($"Valoare necunoscută pentru tipul tranzacției: '{stored}'."),
        };
    }
}
