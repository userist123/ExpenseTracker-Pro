using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;
using ExpenseTrackerPro.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerPro.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly ViewModelFixture _fixture = new();

    public void Dispose()
    {
        _fixture.Dispose();
    }

    // Month navigation

    [Fact]
    public void The_view_model_starts_on_the_current_month()
    {
        var vm = new MainViewModel(_fixture.Transactions, _fixture.Settings, _fixture.Backup, _fixture.Dialogs);

        Assert.Equal(MonthFormatter.FirstOfMonth(DateTime.Today), vm.CurrentMonth);
    }

    [Fact]
    public void MonthTitle_is_romanian_and_capitalized()
    {
        var vm = _fixture.CreateViewModel();

        Assert.Equal("Octombrie 2026", vm.MonthTitle);
    }

    [Fact]
    public void Previous_and_next_move_one_month_across_year_boundaries()
    {
        var vm = _fixture.CreateViewModel();
        vm.CurrentMonth = new DateTime(2026, 1, 1);

        vm.PreviousMonthCommand.Execute(null);
        Assert.Equal(new DateTime(2025, 12, 1), vm.CurrentMonth);
        Assert.Equal("Decembrie 2025", vm.MonthTitle);

        vm.NextMonthCommand.Execute(null);
        vm.NextMonthCommand.Execute(null);
        Assert.Equal(new DateTime(2026, 2, 1), vm.CurrentMonth);
    }

    [Fact]
    public void A_date_in_the_middle_of_a_month_is_normalized_to_the_first()
    {
        var vm = _fixture.CreateViewModel();

        vm.CurrentMonth = new DateTime(2026, 3, 17);

        Assert.Equal(new DateTime(2026, 3, 1), vm.CurrentMonth);
    }

    [Fact]
    public void GoToToday_returns_to_the_current_month()
    {
        var vm = _fixture.CreateViewModel();
        vm.CurrentMonth = new DateTime(2020, 5, 1);

        vm.GoToTodayCommand.Execute(null);

        Assert.Equal(MonthFormatter.FirstOfMonth(DateTime.Today), vm.CurrentMonth);
    }

    [Fact]
    public void Changing_the_month_reloads_transactions_and_summary()
    {
        _fixture.AddExpense("Octombrie", 100m);
        _fixture.Transactions.Add(TransactionType.Expense, "Altele", "Noiembrie", 30m, new DateTime(2026, 11, 2));
        var vm = _fixture.CreateViewModel();
        Assert.Equal(["Octombrie"], vm.Transactions.Select(x => x.Description));
        Assert.Equal(100m, vm.Expenses);

        vm.NextMonthCommand.Execute(null);

        Assert.Equal(["Noiembrie"], vm.Transactions.Select(x => x.Description));
        Assert.Equal(30m, vm.Expenses);
    }

    // Delete confirmation

    [Fact]
    public void Delete_asks_for_confirmation_and_deletes_when_confirmed()
    {
        _fixture.AddExpense("Cafea", 12m);
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();

        vm.DeleteTransactionCommand.Execute(null);

        var (message, _) = Assert.Single(_fixture.Dialogs.Confirmations);
        Assert.Contains("Cafea", message);
        Assert.Empty(_fixture.Transactions.GetMonth(2026, 10));
        Assert.Empty(vm.Transactions);
        Assert.Equal("Tranzacția a fost ștearsă.", vm.StatusMessage);
    }

    [Fact]
    public void Delete_does_nothing_when_the_user_declines()
    {
        _fixture.AddExpense("Cafea");
        _fixture.Dialogs.ConfirmAnswer = false;
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.Single();

        vm.DeleteTransactionCommand.Execute(null);

        Assert.Single(_fixture.Transactions.GetMonth(2026, 10));
        Assert.Single(vm.Transactions);
        Assert.Equal("Ștergerea a fost anulată.", vm.StatusMessage);
    }

    [Fact]
    public void Delete_without_a_selection_does_not_ask()
    {
        var vm = _fixture.CreateViewModel();

        vm.DeleteTransactionCommand.Execute(null);

        Assert.Empty(_fixture.Dialogs.Confirmations);
        Assert.Contains("Selectează", vm.StatusMessage);
    }

    // Budget

    [Fact]
    public void Summary_flags_follow_the_budget_and_the_balance()
    {
        _fixture.Settings.SetMonthlyBudget(1000m);
        _fixture.AddIncome("Salariu", 500m);
        _fixture.AddExpense("Mult", 1200m);

        var vm = _fixture.CreateViewModel();

        Assert.Equal(-200m, vm.RemainingBudget);
        Assert.True(vm.IsOverBudget);
        Assert.Equal(-700m, vm.Balance);
        Assert.True(vm.IsBalanceNegative);
        Assert.Equal(1d, vm.BudgetUsage);
    }

    [Fact]
    public void Within_budget_the_flags_are_off_and_usage_is_a_fraction()
    {
        _fixture.Settings.SetMonthlyBudget(1000m);
        _fixture.AddIncome("Salariu", 5000m);
        _fixture.AddExpense("Puțin", 250m);

        var vm = _fixture.CreateViewModel();

        Assert.False(vm.IsOverBudget);
        Assert.False(vm.IsBalanceNegative);
        Assert.Equal(0.25, vm.BudgetUsage, 6);
        Assert.Equal(750m, vm.RemainingBudget);
    }

    [Fact]
    public void The_budget_editor_starts_with_the_stored_budget()
    {
        _fixture.Settings.SetMonthlyBudget(4200m);

        var vm = _fixture.CreateViewModel();

        Assert.True(AmountParser.TryParse(vm.BudgetInput, out var budget));
        Assert.Equal(4200m, budget);
    }

    [Fact]
    public void SaveBudget_stores_the_budget_and_refreshes_the_summary()
    {
        _fixture.AddExpense("Ceva", 100m);
        var vm = _fixture.CreateViewModel();

        vm.BudgetInput = "250,50";
        vm.SaveBudgetCommand.Execute(null);

        Assert.Equal(250.50m, _fixture.Settings.GetMonthlyBudget());
        Assert.Equal(250.50m, vm.MonthlyBudget);
        Assert.Equal(150.50m, vm.RemainingBudget);
        Assert.Equal("Bugetul lunar a fost salvat.", vm.StatusMessage);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("1.234,56")]
    public void SaveBudget_rejects_invalid_input_and_keeps_the_old_budget(string input)
    {
        var vm = _fixture.CreateViewModel();

        vm.BudgetInput = input;
        vm.SaveBudgetCommand.Execute(null);

        Assert.Equal(SettingsService.DefaultMonthlyBudget, _fixture.Settings.GetMonthlyBudget());
        Assert.Contains("nu este valid", vm.StatusMessage);
    }

    // Search and category filter

    [Fact]
    public void Search_filters_the_list_but_not_the_summary_or_the_chart()
    {
        _fixture.AddExpense("Cafea", 10m, "Alimentație");
        _fixture.AddExpense("Metrou", 20m, "Transport");
        var vm = _fixture.CreateViewModel();

        vm.SearchText = "cafea";

        Assert.Equal(["Cafea"], vm.Transactions.Select(x => x.Description));
        Assert.Equal(30m, vm.Expenses);
        Assert.Equal(2, vm.CategoryExpenses.Count);
        Assert.Equal("1 din 2", vm.TransactionCountText);
    }

    [Fact]
    public void Category_filter_and_search_combine_and_can_be_cleared()
    {
        _fixture.AddExpense("Cafea", 10m, "Alimentație");
        _fixture.AddExpense("Cafea la serviciu", 5m, "Distracție");
        _fixture.AddExpense("Metrou", 20m, "Transport");
        var vm = _fixture.CreateViewModel();

        vm.SelectedCategoryFilter = "Alimentație";
        Assert.Equal(["Cafea"], vm.Transactions.Select(x => x.Description));

        vm.SearchText = "serviciu";
        Assert.Empty(vm.Transactions);

        vm.SearchText = string.Empty;
        vm.SelectedCategoryFilter = TransactionFilter.AllCategoriesLabel;
        Assert.Equal(3, vm.Transactions.Count);
        Assert.Equal("3 din 3", vm.TransactionCountText);
    }

    [Fact]
    public void The_filter_survives_a_month_change()
    {
        _fixture.AddExpense("Cafea");
        _fixture.Transactions.Add(TransactionType.Expense, "Altele", "Cafea nouă", 5m, new DateTime(2026, 11, 3));
        _fixture.Transactions.Add(TransactionType.Expense, "Altele", "Altceva", 5m, new DateTime(2026, 11, 4));
        var vm = _fixture.CreateViewModel();
        vm.SearchText = "cafea";

        vm.NextMonthCommand.Execute(null);

        Assert.Equal(["Cafea nouă"], vm.Transactions.Select(x => x.Description));
    }

    [Fact]
    public void The_category_filter_offers_all_categories_once_after_the_all_entry()
    {
        var vm = _fixture.CreateViewModel();

        Assert.Equal(TransactionFilter.AllCategoriesLabel, vm.FilterCategories[0]);
        Assert.Equal(vm.FilterCategories.Count, vm.FilterCategories.Distinct().Count());
        Assert.All(Core.Services.Categories.Expense, c => Assert.Contains(c, vm.FilterCategories));
        Assert.All(Core.Services.Categories.Income, c => Assert.Contains(c, vm.FilterCategories));
        Assert.Equal(TransactionFilter.AllCategoriesLabel, vm.SelectedCategoryFilter);
    }

    // Chart

    [Fact]
    public void The_chart_lists_expenses_by_category_largest_first()
    {
        _fixture.AddExpense("A", 100m, "Transport");
        _fixture.AddExpense("B", 300m, "Alimentație");
        _fixture.AddIncome("Salariu", 9000m);

        var vm = _fixture.CreateViewModel();

        Assert.True(vm.HasCategoryExpenses);
        Assert.Equal(["Alimentație", "Transport"], vm.CategoryExpenses.Select(x => x.Category));
        Assert.Equal(0.75, vm.CategoryExpenses[0].Share, 6);
    }

    [Fact]
    public void The_chart_is_empty_for_a_month_without_expenses()
    {
        var vm = _fixture.CreateViewModel();

        Assert.False(vm.HasCategoryExpenses);
        Assert.Empty(vm.CategoryExpenses);
    }

    // Add

    [Fact]
    public void Saving_a_new_transaction_adds_it_and_clears_description_and_amount()
    {
        var vm = _fixture.CreateViewModel();
        vm.NewDescription = "Cafea";
        vm.NewAmount = "12,50";
        vm.NewDate = new DateTime(2026, 10, 9);

        vm.SaveTransactionCommand.Execute(null);

        Assert.Equal("Cafea", Assert.Single(vm.Transactions).Description);
        Assert.Equal(12.50m, vm.Expenses);
        Assert.Equal(string.Empty, vm.NewDescription);
        Assert.Equal(string.Empty, vm.NewAmount);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void Changing_the_form_type_limits_the_categories()
    {
        var vm = _fixture.CreateViewModel();

        vm.NewType = TransactionType.Income;

        Assert.Equal(Core.Services.Categories.Income, vm.Categories);
        Assert.Equal("Salariu", vm.NewCategory);
    }

    // Import

    [Fact]
    public void Import_reports_imported_duplicates_and_errors()
    {
        var path = _fixture.FilePath("import.csv");
        File.WriteAllText(
            path,
            CsvExporter.Header + "\r\n" +
            "2026-10-05;\"Cafea\";\"Alimentație\";\"Cheltuială\";12.00\r\n" +
            "2026-10-06;\"Salariu\";\"Salariu\";\"Venit\";5000.00\r\n" +
            "ieri;\"Rău\";\"Altele\";\"Cheltuială\";1.00\r\n");
        _fixture.AddExpense("Cafea", 12m, "Alimentație", day: 5);
        _fixture.Dialogs.OpenPath = path;
        var vm = _fixture.CreateViewModel();

        vm.ImportCsvCommand.Execute(null);

        Assert.Equal("Importate 1, ignorate 1 duplicate, 1 erori.", vm.StatusMessage);
        Assert.Equal(2, vm.Transactions.Count);
        Assert.Equal(5000m, vm.Income);
        var error = Assert.Single(_fixture.Dialogs.Errors);
        Assert.Contains("Linia 4", error);
    }

    [Fact]
    public void Import_without_problems_shows_no_error_dialog()
    {
        var path = _fixture.FilePath("import.csv");
        File.WriteAllText(path, CsvExporter.Header + "\r\n2026-10-05;\"Cafea\";\"Alimentație\";\"Cheltuială\";12.00\r\n");
        _fixture.Dialogs.OpenPath = path;
        var vm = _fixture.CreateViewModel();

        vm.ImportCsvCommand.Execute(null);

        Assert.Equal("Importate 1, ignorate 0 duplicate, 0 erori.", vm.StatusMessage);
        Assert.Empty(_fixture.Dialogs.Errors);
    }

    [Fact]
    public void Import_of_a_file_with_a_wrong_header_imports_nothing_and_shows_the_error()
    {
        var path = _fixture.FilePath("rau.csv");
        File.WriteAllText(path, "a,b,c\r\n1,2,3\r\n");
        _fixture.Dialogs.OpenPath = path;
        var vm = _fixture.CreateViewModel();

        vm.ImportCsvCommand.Execute(null);

        Assert.Equal("Importate 0, ignorate 0 duplicate, 1 erori.", vm.StatusMessage);
        Assert.Contains("Antet", Assert.Single(_fixture.Dialogs.Errors));
        Assert.Empty(vm.Transactions);
    }

    [Fact]
    public void Import_lists_at_most_ten_errors()
    {
        var path = _fixture.FilePath("multe.csv");
        var lines = Enumerable.Range(0, 15).Select(_ => "ieri;\"Rău\";\"Altele\";\"Cheltuială\";1.00");
        File.WriteAllText(path, CsvExporter.Header + "\r\n" + string.Join("\r\n", lines) + "\r\n");
        _fixture.Dialogs.OpenPath = path;
        var vm = _fixture.CreateViewModel();

        vm.ImportCsvCommand.Execute(null);

        Assert.Contains("15 erori", vm.StatusMessage);
        var message = Assert.Single(_fixture.Dialogs.Errors);
        Assert.Contains("Linia 11", message);
        Assert.DoesNotContain("Linia 12", message);
        Assert.Contains("încă 5", message);
    }

    [Fact]
    public void Cancelling_the_import_picker_does_nothing()
    {
        var vm = _fixture.CreateViewModel();
        var before = vm.StatusMessage;

        vm.ImportCsvCommand.Execute(null);

        Assert.Equal(before, vm.StatusMessage);
        Assert.Empty(_fixture.Dialogs.Errors);
    }

    [Fact]
    public void Import_of_a_missing_file_is_reported_without_crashing()
    {
        _fixture.Dialogs.OpenPath = _fixture.FilePath("nu-exista.csv");
        var vm = _fixture.CreateViewModel();

        vm.ImportCsvCommand.Execute(null);

        Assert.Contains("nu a putut fi citit", vm.StatusMessage);
        Assert.Empty(vm.Transactions);
    }

    // Backup and restore

    [Fact]
    public void Backup_offers_a_dated_default_name_and_writes_the_copy()
    {
        _fixture.AddExpense("Cafea");
        var target = _fixture.FilePath("copie.db");
        _fixture.Dialogs.SavePath = target;
        var vm = _fixture.CreateViewModel();

        vm.BackupCommand.Execute(null);

        var request = Assert.Single(_fixture.Dialogs.SaveRequests);
        Assert.Equal($"expense-tracker-backup-{DateTime.Today:yyyyMMdd}.db", request.DefaultName);
        Assert.True(File.Exists(target));
        Assert.Contains("Backup creat", vm.StatusMessage);
    }

    [Fact]
    public void Cancelling_the_backup_dialog_creates_nothing()
    {
        var vm = _fixture.CreateViewModel();

        vm.BackupCommand.Execute(null);

        Assert.Equal("Gata.", vm.StatusMessage);
    }

    [Fact]
    public void Restore_warns_replaces_the_data_and_reloads_everything()
    {
        _fixture.AddExpense("Din backup", 10m);
        _fixture.Settings.SetMonthlyBudget(1111m);
        var backupPath = _fixture.FilePath("backup.db");
        Assert.True(_fixture.Backup.Backup(backupPath).Success);

        _fixture.AddExpense("După backup", 20m);
        _fixture.Settings.SetMonthlyBudget(2222m);
        var vm = _fixture.CreateViewModel();
        vm.SelectedTransaction = vm.Transactions.First();
        vm.EditTransactionCommand.Execute(null);
        Assert.Equal(2, vm.Transactions.Count);

        _fixture.Dialogs.OpenPath = backupPath;
        vm.RestoreCommand.Execute(null);

        var (warning, _) = Assert.Single(_fixture.Dialogs.Confirmations);
        Assert.Contains("înlocuiește", warning);
        Assert.Contains(BackupService.BeforeRestoreFileName, warning);
        Assert.Equal(["Din backup"], vm.Transactions.Select(x => x.Description));
        Assert.Equal(1111m, vm.MonthlyBudget);
        Assert.True(AmountParser.TryParse(vm.BudgetInput, out var budget));
        Assert.Equal(1111m, budget);
        Assert.False(vm.IsEditing);
        Assert.True(File.Exists(_fixture.Backup.BeforeRestorePath));
        Assert.Contains("restaurate", vm.StatusMessage);
    }

    [Fact]
    public void Restore_does_nothing_when_the_user_declines()
    {
        _fixture.AddExpense("Curent");
        var backupPath = _fixture.FilePath("backup.db");
        _fixture.Backup.Backup(backupPath);
        _fixture.AddExpense("Mai nou");
        var vm = _fixture.CreateViewModel();
        _fixture.Dialogs.OpenPath = backupPath;
        _fixture.Dialogs.ConfirmAnswer = false;

        vm.RestoreCommand.Execute(null);

        Assert.Equal(2, _fixture.Transactions.GetMonth(2026, 10).Count);
        Assert.Equal("Restaurarea a fost anulată.", vm.StatusMessage);
        Assert.False(File.Exists(_fixture.Backup.BeforeRestorePath));
    }

    [Fact]
    public void Restore_of_an_invalid_file_shows_an_error_and_keeps_the_data()
    {
        _fixture.AddExpense("Rămâne");
        var bogus = _fixture.FilePath("text.db");
        File.WriteAllText(bogus, "nu e o bază de date");
        _fixture.Dialogs.OpenPath = bogus;
        var vm = _fixture.CreateViewModel();

        vm.RestoreCommand.Execute(null);

        Assert.Single(_fixture.Dialogs.Errors);
        Assert.Equal(["Rămâne"], vm.Transactions.Select(x => x.Description));
        using var db = _fixture.Factory.CreateDbContext();
        Assert.Single(db.Transactions.AsNoTracking().ToList());
    }
}
