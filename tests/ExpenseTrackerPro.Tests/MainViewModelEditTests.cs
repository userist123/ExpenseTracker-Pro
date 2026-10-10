using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;
using ExpenseTrackerPro.ViewModels;

namespace ExpenseTrackerPro.Tests;

public sealed class MainViewModelEditTests : IDisposable
{
    private readonly ViewModelFixture _fixture = new();

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public void A_new_view_model_is_in_add_mode()
    {
        var vm = _fixture.CreateViewModel();

        Assert.False(vm.IsEditing);
        Assert.Equal("Adaugă tranzacție", vm.FormTitle);
        Assert.Equal("Salvează tranzacția", vm.SaveButtonText);
    }

    [Fact]
    public void Edit_loads_the_selected_transaction_into_the_form()
    {
        _fixture.AddExpense("Cafea", 12.5m, "Alimentație", day: 7);
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();

        vm.EditTransactionCommand.Execute(null);

        Assert.True(vm.IsEditing);
        Assert.Equal("Editează tranzacția", vm.FormTitle);
        Assert.Equal("Salvează modificările", vm.SaveButtonText);
        Assert.Equal(TransactionType.Expense, vm.NewType);
        Assert.Equal("Alimentație", vm.NewCategory);
        Assert.Equal("Cafea", vm.NewDescription);
        Assert.Equal(new DateTime(2026, 10, 7), vm.NewDate);
        Assert.True(AmountParser.TryParse(vm.NewAmount, out var amount));
        Assert.Equal(12.5m, amount);
    }

    [Fact]
    public void Edit_of_an_income_keeps_its_category_even_though_the_type_change_resets_categories()
    {
        _fixture.AddIncome("Salariu octombrie", 8000m, "Freelance");
        var vm = _fixture.CreateViewModel();
        Assert.Equal(TransactionType.Expense, vm.NewType);
        vm.SelectedTransaction = vm.Transactions.Single();

        vm.EditTransactionCommand.Execute(null);

        Assert.Equal(TransactionType.Income, vm.NewType);
        Assert.Equal("Freelance", vm.NewCategory);
        Assert.Equal(Core.Services.Categories.Income, vm.Categories);
    }

    [Fact]
    public void Edit_without_a_selection_only_shows_a_message()
    {
        var vm = _fixture.CreateViewModel();

        vm.EditTransactionCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Contains("Selectează", vm.StatusMessage);
    }

    [Fact]
    public void Saving_in_edit_mode_updates_the_same_transaction_and_leaves_edit_mode()
    {
        var id = _fixture.AddExpense("Vechi", 10m, "Altele");
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);

        vm.NewDescription = "Nou";
        vm.NewAmount = "99,90";
        vm.NewCategory = "Transport";
        vm.SaveTransactionCommand.Execute(null);

        var saved = Assert.Single(_fixture.Transactions.GetMonth(2026, 10));
        Assert.Equal(id, saved.Id);
        Assert.Equal("Nou", saved.Description);
        Assert.Equal(99.90m, saved.Amount);
        Assert.Equal("Transport", saved.Category);

        Assert.False(vm.IsEditing);
        Assert.Equal("Adaugă tranzacție", vm.FormTitle);
        Assert.Equal(string.Empty, vm.NewDescription);
        Assert.Equal(string.Empty, vm.NewAmount);
        Assert.Equal("Nou", Assert.Single(vm.Transactions).Description);
        Assert.Equal("Modificările au fost salvate.", vm.StatusMessage);
    }

    [Fact]
    public void Saving_in_edit_mode_can_change_the_type()
    {
        _fixture.AddExpense("Greșit", 50m);
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);

        vm.NewType = TransactionType.Income;
        vm.NewCategory = "Cadouri";
        vm.SaveTransactionCommand.Execute(null);

        var saved = Assert.Single(_fixture.Transactions.GetMonth(2026, 10));
        Assert.Equal(TransactionType.Income, saved.Type);
        Assert.Equal("Cadouri", saved.Category);
        Assert.Equal(50m, vm.Income);
        Assert.Equal(0m, vm.Expenses);
    }

    [Fact]
    public void Saving_in_edit_mode_does_not_create_a_second_transaction()
    {
        _fixture.AddExpense("Unu");
        _fixture.AddExpense("Doi");
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.First();
        vm.EditTransactionCommand.Execute(null);

        vm.SaveTransactionCommand.Execute(null);

        Assert.Equal(2, _fixture.Transactions.GetMonth(2026, 10).Count);
    }

    [Fact]
    public void A_failed_save_keeps_edit_mode_and_the_form_content()
    {
        _fixture.AddExpense("Vechi");
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);

        vm.NewDescription = string.Empty;
        vm.SaveTransactionCommand.Execute(null);

        Assert.True(vm.IsEditing);
        Assert.Contains("descriere", vm.StatusMessage);
        Assert.Equal("Vechi", Assert.Single(_fixture.Transactions.GetMonth(2026, 10)).Description);
    }

    [Fact]
    public void A_bad_amount_keeps_edit_mode()
    {
        _fixture.AddExpense("Vechi");
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);

        vm.NewAmount = "1.234,56";
        vm.SaveTransactionCommand.Execute(null);

        Assert.True(vm.IsEditing);
        Assert.Contains("Suma", vm.StatusMessage);
    }

    [Fact]
    public void Moving_a_transaction_to_another_month_says_where_it_went()
    {
        _fixture.AddExpense("Se mută");
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);

        vm.NewDate = new DateTime(2026, 11, 3);
        vm.SaveTransactionCommand.Execute(null);

        Assert.Empty(vm.Transactions);
        Assert.Contains("Noiembrie 2026", vm.StatusMessage);
        Assert.Single(_fixture.Transactions.GetMonth(2026, 11));
    }

    [Fact]
    public void Cancel_leaves_edit_mode_and_clears_the_form_without_saving()
    {
        _fixture.AddExpense("Vechi");
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);
        vm.NewDescription = "Modificat";

        vm.CancelEditCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Equal(string.Empty, vm.NewDescription);
        Assert.Equal(string.Empty, vm.NewAmount);
        Assert.Equal("Vechi", Assert.Single(_fixture.Transactions.GetMonth(2026, 10)).Description);

        // Saving now adds a new transaction instead of updating the old one.
        vm.NewDescription = "Nou";
        vm.NewAmount = "5";
        vm.SaveTransactionCommand.Execute(null);
        Assert.Equal(2, _fixture.Transactions.GetMonth(2026, 10).Count);
    }

    [Fact]
    public void Changing_the_month_cancels_edit_mode()
    {
        _fixture.AddExpense("Vechi");
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);

        vm.NextMonthCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Equal(string.Empty, vm.NewDescription);

        vm.NewDescription = "Nou";
        vm.NewAmount = "5";
        vm.SaveTransactionCommand.Execute(null);
        Assert.Contains(_fixture.Transactions.GetMonth(2026, 10), x => x.Description == "Vechi" && x.Amount == 10m);
    }

    [Fact]
    public void Deleting_the_edited_transaction_cancels_edit_mode()
    {
        _fixture.AddExpense("De șters");
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);

        vm.DeleteTransactionCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Empty(vm.Transactions);
        Assert.Equal(string.Empty, vm.NewDescription);
    }

    [Fact]
    public void Deleting_another_transaction_keeps_edit_mode()
    {
        _fixture.AddExpense("Editat", day: 6);
        _fixture.AddExpense("Altul", day: 5);
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single(x => x.Description == "Editat");
        vm.EditTransactionCommand.Execute(null);

        vm.SelectedTransaction = vm.Transactions.Single(x => x.Description == "Altul");
        vm.DeleteTransactionCommand.Execute(null);

        Assert.True(vm.IsEditing);
        Assert.Equal("Editat", vm.NewDescription);
        Assert.Equal("Editat", Assert.Single(vm.Transactions).Description);
    }

    [Fact]
    public void Editing_another_row_while_editing_switches_the_form_to_it()
    {
        var first = _fixture.AddExpense("Primul", day: 6);
        _fixture.AddExpense("Al doilea", day: 5);
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single(x => x.Description == "Primul");
        vm.EditTransactionCommand.Execute(null);

        vm.SelectedTransaction = vm.Transactions.Single(x => x.Description == "Al doilea");
        vm.EditTransactionCommand.Execute(null);
        vm.NewDescription = "Al doilea modificat";
        vm.SaveTransactionCommand.Execute(null);

        var saved = _fixture.Transactions.GetMonth(2026, 10);
        Assert.Contains(saved, x => x.Id == first && x.Description == "Primul");
        Assert.Contains(saved, x => x.Description == "Al doilea modificat");
    }

    [Fact]
    public void A_legacy_category_is_still_shown_when_editing_and_a_warning_is_displayed()
    {
        _fixture.AddLegacy(TransactionType.Expense, "Salariu", "Rând vechi", 40m);
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();

        vm.EditTransactionCommand.Execute(null);

        Assert.Equal("Salariu", vm.NewCategory);
        Assert.Contains("Salariu", vm.Categories);
        Assert.Equal(Core.Services.Categories.Expense.Count + 1, vm.Categories.Count);
        Assert.Contains("nu mai este validă", vm.StatusMessage);
    }

    [Fact]
    public void A_legacy_category_cannot_be_saved_until_a_valid_one_is_chosen()
    {
        var id = _fixture.AddLegacy(TransactionType.Expense, "Salariu", "Rând vechi", 40m);
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);

        vm.SaveTransactionCommand.Execute(null);

        Assert.True(vm.IsEditing);
        Assert.Contains("Categoria", vm.StatusMessage);
        Assert.Equal("Salariu", _fixture.Transactions.GetMonth(2026, 10).Single(x => x.Id == id).Category);

        vm.NewCategory = "Altele";
        vm.SaveTransactionCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Equal("Altele", _fixture.Transactions.GetMonth(2026, 10).Single(x => x.Id == id).Category);
    }

    [Fact]
    public void Switching_the_type_while_editing_rebuilds_the_category_list()
    {
        _fixture.AddLegacy(TransactionType.Income, "Transport", "Rând vechi", 40m);
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();
        vm.EditTransactionCommand.Execute(null);
        Assert.Equal("Transport", vm.NewCategory);
        Assert.Equal(Core.Services.Categories.Income.Count + 1, vm.Categories.Count);

        vm.NewType = TransactionType.Expense;

        Assert.Equal(Core.Services.Categories.Expense, vm.Categories);
        Assert.Equal(Core.Services.Categories.Expense[0], vm.NewCategory);
    }
}
