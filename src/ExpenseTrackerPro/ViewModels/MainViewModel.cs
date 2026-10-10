using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;
using ExpenseTrackerPro.Services;
using CategoryCatalog = ExpenseTrackerPro.Core.Services.Categories;

namespace ExpenseTrackerPro.ViewModels;

// Keep this class free of WPF types: the test project compiles it directly (without WPF) and drives it with a fake
// IDialogService.
public partial class MainViewModel : ObservableObject
{
    private const string CsvFilter = "Fișiere CSV (*.csv)|*.csv|Toate fișierele (*.*)|*.*";
    private const string DatabaseFilter = "Bază de date SQLite (*.db)|*.db|Toate fișierele (*.*)|*.*";
    private const int MaxErrorLinesShown = 10;

    private readonly TransactionService _transactionService;
    private readonly SettingsService _settingsService;
    private readonly BackupService _backupService;
    private readonly IDialogService _dialogs;

    // Every transaction of the current month; Transactions holds the filtered subset that is displayed.
    private IReadOnlyList<TransactionItem> _monthItems = [];
    private Guid? _editingId;

    public MainViewModel(
        TransactionService transactionService,
        SettingsService settingsService,
        BackupService backupService,
        IDialogService dialogs)
    {
        _transactionService = transactionService;
        _settingsService = settingsService;
        _backupService = backupService;
        _dialogs = dialogs;

        FilterCategories = [TransactionFilter.AllCategoriesLabel, .. CategoryCatalog.All];
        BudgetInput = FormatBudget(_settingsService.GetMonthlyBudget());

        RefreshCategories();
        LoadTransactions();
    }

    /// <summary>The transactions shown in the grid (the month's list after search and category filter).</summary>
    public ObservableCollection<TransactionItem> Transactions { get; } = [];

    /// <summary>Categories valid for the type selected in the form.</summary>
    public ObservableCollection<string> Categories { get; } = [];

    /// <summary>"Toate categoriile" followed by every category, for the list filter.</summary>
    public IReadOnlyList<string> FilterCategories { get; }

    /// <summary>Expenses of the month per category, largest first, for the chart.</summary>
    public ObservableCollection<CategoryExpense> CategoryExpenses { get; } = [];

    public IReadOnlyList<TransactionTypeOption> TransactionTypes { get; } =
    [
        new(TransactionType.Expense, TransactionType.Expense.ToDisplayName()),
        new(TransactionType.Income, TransactionType.Income.ToDisplayName()),
    ];

    public string MonthTitle => MonthFormatter.Title(CurrentMonth);

    public string FormTitle => IsEditing ? "Editează tranzacția" : "Adaugă tranzacție";

    public string SaveButtonText => IsEditing ? "Salvează modificările" : "Salvează tranzacția";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthTitle))]
    private DateTime currentMonth = MonthFormatter.FirstOfMonth(DateTime.Today);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    private bool isEditing;

    [ObservableProperty]
    private TransactionType newType = TransactionType.Expense;

    [ObservableProperty]
    private string? newCategory;

    [ObservableProperty]
    private string newDescription = string.Empty;

    [ObservableProperty]
    private string newAmount = string.Empty;

    [ObservableProperty]
    private DateTime? newDate = DateTime.Today;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedCategoryFilter = TransactionFilter.AllCategoriesLabel;

    [ObservableProperty]
    private string transactionCountText = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Gata.";

    [ObservableProperty]
    private TransactionItem? selectedTransaction;

    [ObservableProperty]
    private decimal income;

    [ObservableProperty]
    private decimal expenses;

    [ObservableProperty]
    private decimal balance;

    [ObservableProperty]
    private decimal monthlyBudget;

    [ObservableProperty]
    private decimal remainingBudget;

    /// <summary>Share of the budget spent, 0-1 (clamped), for the progress bar.</summary>
    [ObservableProperty]
    private double budgetUsage;

    [ObservableProperty]
    private bool isOverBudget;

    [ObservableProperty]
    private bool isBalanceNegative;

    [ObservableProperty]
    private bool hasCategoryExpenses;

    /// <summary>Text of the budget editor next to the budget progress bar.</summary>
    [ObservableProperty]
    private string budgetInput = string.Empty;

    partial void OnCurrentMonthChanged(DateTime value)
    {
        var firstOfMonth = MonthFormatter.FirstOfMonth(value);
        if (value != firstOfMonth)
        {
            CurrentMonth = firstOfMonth;
            return;
        }

        var wasEditing = IsEditing;
        if (wasEditing)
        {
            ResetForm();
        }

        LoadTransactions();

        if (wasEditing)
        {
            StatusMessage = "Editarea a fost anulată deoarece ai schimbat luna.";
        }
    }

    partial void OnNewTypeChanged(TransactionType value)
    {
        RefreshCategories();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryFilterChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
    }

    [RelayCommand]
    private void NextMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
    }

    [RelayCommand]
    private void GoToToday()
    {
        CurrentMonth = MonthFormatter.FirstOfMonth(DateTime.Today);
    }

    /// <summary>Adds a new transaction, or saves the changes when a transaction is being edited.</summary>
    [RelayCommand]
    private void SaveTransaction()
    {
        if (!AmountParser.TryParse(NewAmount, out var amount))
        {
            StatusMessage = "Suma introdusă nu este validă. Folosește cifre și cel mult 2 zecimale (ex: 12,50).";
            return;
        }

        if (NewDate is null)
        {
            StatusMessage = "Selectează data tranzacției.";
            return;
        }

        var date = NewDate.Value;
        var category = NewCategory ?? string.Empty;

        if (_editingId is { } id)
        {
            var updated = _transactionService.Update(id, NewType, category, NewDescription, amount, date);
            if (!updated.Success)
            {
                StatusMessage = updated.Error ?? "Modificările nu au putut fi salvate.";
                return;
            }

            ResetForm();
            StatusMessage = MonthFormatter.FirstOfMonth(date) == CurrentMonth
                ? "Modificările au fost salvate."
                : $"Modificările au fost salvate. Tranzacția aparține acum lunii {MonthFormatter.Title(date)}.";
            LoadTransactions();
            return;
        }

        var added = _transactionService.Add(NewType, category, NewDescription, amount, date);
        if (!added.Success)
        {
            StatusMessage = added.Error ?? "Tranzacția nu a putut fi salvată.";
            return;
        }

        NewDescription = string.Empty;
        NewAmount = string.Empty;
        StatusMessage = "Tranzacția a fost salvată local.";
        LoadTransactions();
    }

    [RelayCommand]
    private void EditTransaction()
    {
        if (SelectedTransaction is null)
        {
            StatusMessage = "Selectează o tranzacție pentru editare.";
            return;
        }

        BeginEdit(SelectedTransaction);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (!IsEditing)
        {
            return;
        }

        ResetForm();
        StatusMessage = "Editarea a fost anulată.";
    }

    [RelayCommand]
    private void DeleteTransaction()
    {
        var item = SelectedTransaction;
        if (item is null)
        {
            StatusMessage = "Selectează o tranzacție pentru ștergere.";
            return;
        }

        var question =
            $"Sigur vrei să ștergi tranzacția „{item.Description}” ({item.Amount:N2} RON, {item.Date:dd.MM.yyyy})?";
        if (!_dialogs.Confirm(question, "Confirmare ștergere"))
        {
            StatusMessage = "Ștergerea a fost anulată.";
            return;
        }

        var result = _transactionService.Delete(item.Id);
        if (_editingId == item.Id)
        {
            ResetForm();
        }

        StatusMessage = result.Success
            ? "Tranzacția a fost ștearsă."
            : result.Error ?? "Tranzacția nu a putut fi ștearsă.";

        LoadTransactions();
    }

    [RelayCommand]
    private void SaveBudget()
    {
        if (!AmountParser.TryParse(BudgetInput, out var budget))
        {
            StatusMessage = "Bugetul introdus nu este valid. Folosește cifre și cel mult 2 zecimale (ex: 7500).";
            return;
        }

        var result = _settingsService.SetMonthlyBudget(budget);
        if (!result.Success)
        {
            StatusMessage = result.Error ?? "Bugetul nu a putut fi salvat.";
            return;
        }

        BudgetInput = FormatBudget(budget);
        StatusMessage = "Bugetul lunar a fost salvat.";
        LoadTransactions();
    }

    /// <summary>Exports the whole current month (not just the rows left by the search filter).</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ExpenseTrackerPro");

        try
        {
            var path = CsvExporter.ExportMonth(_monthItems, folder, CurrentMonth.Year, CurrentMonth.Month);
            StatusMessage = $"Export creat: {path}";
        }
        catch (IOException)
        {
            StatusMessage = "Exportul nu a putut fi creat (eroare de scriere a fișierului).";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Exportul nu a putut fi creat: nu ai drepturi de scriere în folderul Documente.";
        }
    }

    [RelayCommand]
    private void ImportCsv()
    {
        var path = _dialogs.PickOpenFile(CsvFilter);
        if (path is null)
        {
            return;
        }

        CsvParseResult parsed;
        try
        {
            parsed = CsvImporter.ParseFile(path);
        }
        catch (IOException)
        {
            StatusMessage = "Fișierul nu a putut fi citit (eroare de citire).";
            return;
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Fișierul nu a putut fi citit: nu ai drepturi de acces.";
            return;
        }

        var report = parsed.Rows.Count > 0
            ? _transactionService.ImportMany(parsed.Rows)
            : new ImportReport(0, 0, []);

        var errors = parsed.Errors.Concat(report.Errors).OrderBy(x => x.LineNumber).ToList();
        StatusMessage = $"Importate {report.Imported}, ignorate {report.Duplicates} duplicate, {errors.Count} erori.";

        if (errors.Count > 0)
        {
            _dialogs.ShowError(BuildImportErrorMessage(errors));
        }

        LoadTransactions();
    }

    [RelayCommand]
    private void Backup()
    {
        var defaultName = $"expense-tracker-backup-{DateTime.Today:yyyyMMdd}.db";
        var path = _dialogs.PickSaveFile(DatabaseFilter, defaultName);
        if (path is null)
        {
            return;
        }

        var result = _backupService.Backup(path);
        StatusMessage = result.Success
            ? $"Backup creat: {path}"
            : result.Error ?? "Backup-ul nu a putut fi creat.";
    }

    [RelayCommand]
    private void Restore()
    {
        var path = _dialogs.PickOpenFile(DatabaseFilter);
        if (path is null)
        {
            return;
        }

        var warning =
            "Restaurarea înlocuiește TOATE datele curente cu cele din fișierul selectat." + Environment.NewLine +
            $"O copie a datelor curente se salvează în „{BackupService.BeforeRestoreFileName}”." + Environment.NewLine +
            Environment.NewLine +
            "Continui?";
        if (!_dialogs.Confirm(warning, "Confirmare restaurare"))
        {
            StatusMessage = "Restaurarea a fost anulată.";
            return;
        }

        var result = _backupService.Restore(path);
        if (!result.Success)
        {
            StatusMessage = result.Error ?? "Restaurarea nu a putut fi făcută.";
            _dialogs.ShowError(StatusMessage);
            return;
        }

        ResetForm();
        BudgetInput = FormatBudget(_settingsService.GetMonthlyBudget());
        LoadTransactions();
        StatusMessage = $"Datele au fost restaurate. Copia datelor anterioare: {_backupService.BeforeRestorePath}";
    }

    /// <summary>Loads a transaction into the form and switches it to edit mode.</summary>
    private void BeginEdit(TransactionItem item)
    {
        _editingId = item.Id;
        IsEditing = true;

        // The type first: changing it resets the category list.
        NewType = item.Type;
        RefreshCategories(item.Category);
        NewDescription = item.Description;
        NewAmount = item.Amount.ToString("0.00", CultureInfo.CurrentCulture);
        NewDate = item.Date;

        StatusMessage = CategoryCatalog.IsValid(item.Type, item.Category)
            ? "Editezi tranzacția selectată. Salvează modificările sau renunță."
            : $"Categoria „{item.Category}” nu mai este validă pentru tipul „{item.Type.ToDisplayName()}”. " +
              "Alege o categorie nouă înainte de a salva.";
    }

    /// <summary>Leaves edit mode and puts the form back to its "add" defaults.</summary>
    private void ResetForm()
    {
        _editingId = null;
        IsEditing = false;
        NewDescription = string.Empty;
        NewAmount = string.Empty;
        NewDate = DateTime.Today;
        NewType = TransactionType.Expense;
        RefreshCategories();
    }

    /// <summary>
    /// Shows only the categories valid for the selected type. <paramref name="preferred"/> is selected when given;
    /// if it is not valid for the type (legacy data) it is appended so the stored value stays visible, and saving is
    /// then rejected by the service until another category is chosen.
    /// </summary>
    private void RefreshCategories(string? preferred = null)
    {
        Categories.Clear();
        foreach (var category in CategoryCatalog.For(NewType))
        {
            Categories.Add(category);
        }

        if (!string.IsNullOrEmpty(preferred) && !Categories.Contains(preferred))
        {
            Categories.Add(preferred);
        }

        NewCategory = string.IsNullOrEmpty(preferred) ? Categories.FirstOrDefault() : preferred;
    }

    private void LoadTransactions()
    {
        var items = _transactionService.GetMonth(CurrentMonth.Year, CurrentMonth.Month);
        _monthItems = items;

        var summary = _transactionService.GetSummary(items);
        Income = summary.Income;
        Expenses = summary.Expenses;
        Balance = summary.Balance;
        MonthlyBudget = summary.Budget;
        RemainingBudget = summary.RemainingBudget;
        BudgetUsage = summary.BudgetUsage;
        IsOverBudget = summary.IsOverBudget;
        IsBalanceNegative = summary.IsBalanceNegative;

        CategoryExpenses.Clear();
        foreach (var entry in ExpenseBreakdown.From(items))
        {
            CategoryExpenses.Add(entry);
        }

        HasCategoryExpenses = CategoryExpenses.Count > 0;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = TransactionFilter.Apply(_monthItems, SearchText, SelectedCategoryFilter);

        Transactions.Clear();
        foreach (var item in filtered)
        {
            Transactions.Add(item);
        }

        TransactionCountText = $"{filtered.Count} din {_monthItems.Count}";
        SelectedTransaction = null;
    }

    private static string FormatBudget(decimal budget)
    {
        return budget.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static string BuildImportErrorMessage(IReadOnlyList<ImportError> errors)
    {
        var lines = errors.Take(MaxErrorLinesShown).Select(x => x.ToString()).ToList();
        if (errors.Count > MaxErrorLinesShown)
        {
            lines.Add($"... și încă {errors.Count - MaxErrorLinesShown}.");
        }

        return "Unele rânduri nu au putut fi importate:" + Environment.NewLine + Environment.NewLine +
               string.Join(Environment.NewLine, lines);
    }
}
