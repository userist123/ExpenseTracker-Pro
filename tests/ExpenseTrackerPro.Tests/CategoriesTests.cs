using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public class CategoriesTests
{
    [Fact]
    public void Expense_categories_do_not_contain_salary()
    {
        Assert.DoesNotContain("Salariu", Categories.For(TransactionType.Expense));
        Assert.Contains("Alimentație", Categories.For(TransactionType.Expense));
    }

    [Fact]
    public void Income_categories_do_not_contain_groceries()
    {
        Assert.DoesNotContain("Alimentație", Categories.For(TransactionType.Income));
        Assert.Contains("Salariu", Categories.For(TransactionType.Income));
    }

    [Theory]
    [InlineData(TransactionType.Income, "Salariu", true)]
    [InlineData(TransactionType.Income, "Altele", true)]
    [InlineData(TransactionType.Expense, "Altele", true)]
    [InlineData(TransactionType.Expense, "Salariu", false)]
    [InlineData(TransactionType.Income, "Transport", false)]
    [InlineData(TransactionType.Expense, "alimentație", false)] // case-sensitive: values come from the fixed lists
    [InlineData(TransactionType.Expense, "", false)]
    [InlineData(TransactionType.Expense, null, false)]
    public void IsValid_checks_category_against_type(TransactionType type, string? category, bool expected)
    {
        Assert.Equal(expected, Categories.IsValid(type, category));
    }

    [Theory]
    [InlineData(TransactionType.Income)]
    [InlineData(TransactionType.Expense)]
    public void Default_is_a_valid_category(TransactionType type)
    {
        Assert.True(Categories.IsValid(type, Categories.Default(type)));
    }
}
