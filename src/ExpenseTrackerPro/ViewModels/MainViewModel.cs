using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;
using CategoryCatalog = ExpenseTrackerPro.Core.Services.Categories;

namespace ExpenseTrackerPro.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TransactionService _transactionService;

    public MainViewModel(TransactionService transactionService)
    {
        _transactionService = transactionService;

        RefreshCategories();
        LoadTransactions();
    }

    public ObservableCollection<TransactionItem> Transactions { get; } = [];

    public ObservableCollection<string> Categories { get; } = [];

    public IReadOnlyList<TransactionTypeOption> TransactionTypes { get; } =
    [
        new(TransactionType.Expense, TransactionType.Expense.ToDisplayName()),
        new(TransactionType.Income, TransactionType.Income.ToDisplayName()),
    ];

    [ObservableProperty]
    private DateTime? selectedDate = DateTime.Today;

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

    partial void OnSelectedDateChanged(DateTime? value)
    {
        LoadTransactions();
    }

    partial void OnNewTypeChanged(TransactionType value)
    {
        RefreshCategories();
    }

    [RelayCommand]
    private void AddTransaction()
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

        var result = _transactionService.Add(NewType, NewCategory ?? string.Empty, NewDescription, amount, NewDate.Value);
        if (!result.Success)
        {
            StatusMessage = result.Error ?? "Tranzacția nu a putut fi salvată.";
            return;
        }

        NewDescription = string.Empty;
        NewAmount = string.Empty;
        StatusMessage = "Tranzacția a fost salvată local.";
        LoadTransactions();
    }

    [RelayCommand]
    private void DeleteTransaction()
    {
        if (SelectedTransaction is null)
        {
            StatusMessage = "Selectează o tranzacție pentru ștergere.";
            return;
        }

        var result = _transactionService.Delete(SelectedTransaction.Id);
        StatusMessage = result.Success
            ? "Tranzacția a fost ștearsă."
            : result.Error ?? "Tranzacția nu a putut fi ștearsă.";

        LoadTransactions();
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var reference = SelectedDate ?? DateTime.Today;
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ExpenseTrackerPro");

        try
        {
            var path = CsvExporter.ExportMonth(Transactions, folder, reference.Year, reference.Month);
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

    /// <summary>Shows only the categories valid for the selected type and selects the first one.</summary>
    private void RefreshCategories()
    {
        Categories.Clear();
        foreach (var category in CategoryCatalog.For(NewType))
        {
            Categories.Add(category);
        }

        NewCategory = Categories.FirstOrDefault();
    }

    private void LoadTransactions()
    {
        var reference = SelectedDate ?? DateTime.Today;
        var items = _transactionService.GetMonth(reference.Year, reference.Month);

        Transactions.Clear();
        foreach (var item in items)
        {
            Transactions.Add(item);
        }

        var summary = _transactionService.GetSummary(reference.Year, reference.Month);
        Income = summary.Income;
        Expenses = summary.Expenses;
        Balance = summary.Balance;
        MonthlyBudget = summary.Budget;
        RemainingBudget = summary.RemainingBudget;
        SelectedTransaction = null;
    }
}
