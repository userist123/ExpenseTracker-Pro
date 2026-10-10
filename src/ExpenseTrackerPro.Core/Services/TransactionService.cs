using ExpenseTrackerPro.Core.Data;
using ExpenseTrackerPro.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerPro.Core.Services;

public sealed class TransactionService
{
    public const int MaxDescriptionLength = 120;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SettingsService _settings;

    public TransactionService(IDbContextFactory<AppDbContext> factory, SettingsService settings)
    {
        _factory = factory;
        _settings = settings;
    }

    /// <summary>Transactions of the given month, newest first.</summary>
    public IReadOnlyList<TransactionItem> GetMonth(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        using var db = _factory.CreateDbContext();
        return db.Transactions
            .AsNoTracking()
            .Where(x => x.Date >= start && x.Date < end)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToList();
    }

    public MonthlySummary GetSummary(int year, int month)
    {
        return GetSummary(GetMonth(year, month));
    }

    /// <summary>Totals of already loaded items, so callers that hold the month's list do not query it again.</summary>
    public MonthlySummary GetSummary(IReadOnlyList<TransactionItem> items)
    {
        return MonthlySummary.From(items, _settings.GetMonthlyBudget());
    }

    /// <summary>Expenses of the month grouped by category, largest first (empty when there are none).</summary>
    public IReadOnlyList<CategoryExpense> GetExpensesByCategory(int year, int month)
    {
        return ExpenseBreakdown.From(GetMonth(year, month));
    }

    public OperationResult<TransactionItem> Add(
        TransactionType type,
        string category,
        string description,
        decimal amount,
        DateTime date)
    {
        var validation = Validate(type, category, description, amount);
        if (validation is not null)
        {
            return OperationResult<TransactionItem>.Fail(validation);
        }

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            Type = type,
            Category = category,
            Description = description.Trim(),
            Amount = decimal.Round(amount, 2),
            Date = date.Date,
            CreatedAtUtc = DateTime.UtcNow,
        };

        using var db = _factory.CreateDbContext();
        db.Transactions.Add(item);
        db.SaveChanges();

        return OperationResult<TransactionItem>.Ok(item);
    }

    public OperationResult<TransactionItem> Update(
        Guid id,
        TransactionType type,
        string category,
        string description,
        decimal amount,
        DateTime date)
    {
        var validation = Validate(type, category, description, amount);
        if (validation is not null)
        {
            return OperationResult<TransactionItem>.Fail(validation);
        }

        using var db = _factory.CreateDbContext();
        var item = db.Transactions.Find(id);
        if (item is null)
        {
            return OperationResult<TransactionItem>.Fail("Tranzacția nu mai există.");
        }

        item.Type = type;
        item.Category = category;
        item.Description = description.Trim();
        item.Amount = decimal.Round(amount, 2);
        item.Date = date.Date;
        db.SaveChanges();

        return OperationResult<TransactionItem>.Ok(item);
    }

    /// <summary>
    /// Inserts the valid rows in a single <c>SaveChanges</c>. Rows that fail the same validation as
    /// <see cref="Add"/> are reported as errors. A row is a duplicate when a transaction with the same date, type,
    /// category, description (trimmed) and amount already exists; identical rows inside the import are matched
    /// one-to-one against the existing ones, so importing the same file twice adds nothing the second time while
    /// two genuinely equal purchases in one file are both kept.
    /// </summary>
    public ImportReport ImportMany(IEnumerable<ImportRow> rows)
    {
        var errors = new List<ImportError>();
        var valid = new List<ImportRow>();

        foreach (var row in rows)
        {
            var validation = Validate(row.Type, row.Category, row.Description, row.Amount);
            if (validation is null)
            {
                valid.Add(row);
            }
            else
            {
                errors.Add(new ImportError(row.LineNumber, validation));
            }
        }

        if (valid.Count == 0)
        {
            return new ImportReport(0, 0, errors);
        }

        var from = valid.Min(x => x.Date.Date);
        var to = valid.Max(x => x.Date.Date).AddDays(1);

        using var db = _factory.CreateDbContext();
        var existing = db.Transactions
            .AsNoTracking()
            .Where(x => x.Date >= from && x.Date < to)
            .AsEnumerable()
            .GroupBy(x => DuplicateKey(x.Date, x.Type, x.Category, x.Description, x.Amount))
            .ToDictionary(group => group.Key, group => group.Count());

        var imported = 0;
        var duplicates = 0;
        var now = DateTime.UtcNow;

        foreach (var row in valid)
        {
            var key = DuplicateKey(row.Date, row.Type, row.Category, row.Description, row.Amount);
            if (existing.TryGetValue(key, out var remaining) && remaining > 0)
            {
                existing[key] = remaining - 1;
                duplicates++;
                continue;
            }

            db.Transactions.Add(new TransactionItem
            {
                Id = Guid.NewGuid(),
                Type = row.Type,
                Category = row.Category,
                Description = row.Description.Trim(),
                Amount = decimal.Round(row.Amount, 2),
                Date = row.Date.Date,
                CreatedAtUtc = now,
            });
            imported++;
        }

        db.SaveChanges();
        return new ImportReport(imported, duplicates, errors);
    }

    public OperationResult Delete(Guid id)
    {
        using var db = _factory.CreateDbContext();
        var item = db.Transactions.Find(id);
        if (item is null)
        {
            return OperationResult.Fail("Tranzacția nu mai există.");
        }

        db.Transactions.Remove(item);
        db.SaveChanges();

        return OperationResult.Ok();
    }

    private static (DateTime Date, TransactionType Type, string Category, string Description, decimal Amount) DuplicateKey(
        DateTime date,
        TransactionType type,
        string category,
        string description,
        decimal amount)
    {
        return (date.Date, type, category, description.Trim(), decimal.Round(amount, 2));
    }

    /// <summary>Returns a Romanian error message, or null when the input is valid.</summary>
    private static string? Validate(TransactionType type, string category, string description, decimal amount)
    {
        var trimmed = description?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > MaxDescriptionLength)
        {
            return $"Introdu o descriere între 1 și {MaxDescriptionLength} de caractere.";
        }

        var rounded = decimal.Round(amount, 2);
        if (rounded <= 0m || rounded > AmountParser.MaxAmount)
        {
            return "Suma trebuie să fie pozitivă și cel mult 10.000.000 RON.";
        }

        if (!Categories.IsValid(type, category))
        {
            return $"Categoria „{category}” nu este validă pentru tipul „{type.ToDisplayName()}”.";
        }

        return null;
    }
}
