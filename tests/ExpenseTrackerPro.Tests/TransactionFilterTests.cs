using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;

namespace ExpenseTrackerPro.Tests;

public class TransactionFilterTests
{
    private static IReadOnlyList<TransactionItem> Sample() =>
    [
        TestItems.Expense("Alimentație", "Cumpărături Kaufland", 120m),
        TestItems.Expense("Sănătate", "Ședință la medic", 200m),
        TestItems.Expense("Transport", "Abonament metrou", 80m),
        TestItems.Income("Salariu", "Salariu octombrie", 8000m),
        TestItems.Expense("Alimentație", "Pâine și lapte", 15m),
    ];

    [Fact]
    public void No_search_and_no_category_returns_everything_in_order()
    {
        var items = Sample();

        var result = TransactionFilter.Apply(items, null, null);

        Assert.Equal(items, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_search_does_not_filter(string search)
    {
        Assert.Equal(5, TransactionFilter.Apply(Sample(), search, null).Count);
    }

    [Theory]
    [InlineData("kaufland")]
    [InlineData("KAUFLAND")]
    [InlineData("  Kaufland  ")]
    public void Search_is_case_insensitive_and_trimmed(string search)
    {
        var result = TransactionFilter.Apply(Sample(), search, null);

        Assert.Equal(["Cumpărături Kaufland"], result.Select(x => x.Description));
    }

    [Theory]
    [InlineData("sedinta")]
    [InlineData("ședință")]
    [InlineData("SEDINTA")]
    [InlineData("şedinţă")] // cedilla forms still typed on many keyboards
    public void Search_ignores_diacritics_in_both_directions(string search)
    {
        var result = TransactionFilter.Apply(Sample(), search, null);

        Assert.Equal(["Ședință la medic"], result.Select(x => x.Description));
    }

    [Fact]
    public void Search_with_diacritics_matches_plain_descriptions()
    {
        var items = new[] { TestItems.Expense("Altele", "Cumparaturi", 10m) };

        Assert.Single(TransactionFilter.Apply(items, "cumpărături", null));
    }

    [Fact]
    public void Search_matches_inside_the_description()
    {
        var result = TransactionFilter.Apply(Sample(), "metr", null);

        Assert.Equal(["Abonament metrou"], result.Select(x => x.Description));
    }

    [Fact]
    public void Search_without_matches_returns_an_empty_list()
    {
        Assert.Empty(TransactionFilter.Apply(Sample(), "inexistent", null));
    }

    [Fact]
    public void Category_filter_keeps_only_that_category()
    {
        var result = TransactionFilter.Apply(Sample(), null, "Alimentație");

        Assert.Equal(["Cumpărături Kaufland", "Pâine și lapte"], result.Select(x => x.Description));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(TransactionFilter.AllCategoriesLabel)]
    public void No_category_or_the_all_label_does_not_filter_by_category(string? category)
    {
        Assert.Equal(5, TransactionFilter.Apply(Sample(), null, category).Count);
    }

    [Fact]
    public void Category_filter_is_exact_not_diacritic_insensitive()
    {
        Assert.Empty(TransactionFilter.Apply(Sample(), null, "Alimentatie"));
    }

    [Fact]
    public void Search_and_category_are_combined_with_and()
    {
        var result = TransactionFilter.Apply(Sample(), "lapte", "Alimentație");
        var none = TransactionFilter.Apply(Sample(), "lapte", "Transport");

        Assert.Equal(["Pâine și lapte"], result.Select(x => x.Description));
        Assert.Empty(none);
    }

    [Fact]
    public void Filtering_does_not_modify_the_source()
    {
        var items = Sample();

        TransactionFilter.Apply(items, "metrou", "Transport");

        Assert.Equal(5, items.Count);
    }
}
