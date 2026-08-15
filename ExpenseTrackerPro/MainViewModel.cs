using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerPro;

public partial class MainViewModel : ObservableObject
{
    private const decimal MonthlyBudget = 7500m;
    private readonly AppDbContext _db;
    public ObservableCollection<TransactionItem> Transactions { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["Salariu", "Alimentație", "Utilități", "Transport", "Sănătate", "Distracție", "Investiții", "Altele"];
    public ObservableCollection<string> TransactionTypes { get; } = ["Cheltuială", "Venit"];
    [ObservableProperty] private DateTime? selectedDate = DateTime.Today;
    [ObservableProperty] private string newType = "Cheltuială";
    [ObservableProperty] private string newCategory = "Alimentație";
    [ObservableProperty] private string newDescription = string.Empty;
    [ObservableProperty] private string newAmount = string.Empty;
    [ObservableProperty] private DateTime? newDate = DateTime.Today;
    [ObservableProperty] private string statusMessage = "Gata.";
    [ObservableProperty] private TransactionItem? selectedTransaction;
    [ObservableProperty] private decimal income;
    [ObservableProperty] private decimal expenses;
    [ObservableProperty] private decimal balance;
    [ObservableProperty] private decimal remainingBudget;

    public MainViewModel()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExpenseTrackerPro");
        Directory.CreateDirectory(folder);
        _db = new AppDbContext(Path.Combine(folder, "expense-tracker.db"));
        _db.Database.EnsureCreated();
        SeedIfEmpty();
        LoadTransactions();
    }
    partial void OnSelectedDateChanged(DateTime? value) => LoadTransactions();
    [RelayCommand] private void AddTransaction()
    {
        if (string.IsNullOrWhiteSpace(NewDescription) || NewDescription.Trim().Length > 120) { StatusMessage = "Introdu o descriere între 1 și 120 de caractere."; return; }
        if (!decimal.TryParse(NewAmount, NumberStyles.Number, CultureInfo.GetCultureInfo("ro-RO"), out var amount) && !decimal.TryParse(NewAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)) { StatusMessage = "Suma introdusă nu este validă."; return; }
        if (amount <= 0 || amount > 10_000_000m || NewDate is null) { StatusMessage = "Suma trebuie să fie pozitivă și data trebuie selectată."; return; }
        _db.Transactions.Add(new TransactionItem { Id = Guid.NewGuid(), Type = NewType, Category = NewCategory, Description = NewDescription.Trim(), Amount = decimal.Round(amount, 2), Date = NewDate.Value.Date, CreatedAtUtc = DateTime.UtcNow });
        _db.SaveChanges(); NewDescription = string.Empty; NewAmount = string.Empty; StatusMessage = "Tranzacția a fost salvată local."; LoadTransactions();
    }
    [RelayCommand] private void DeleteTransaction()
    {
        if (SelectedTransaction is null) { StatusMessage = "Selectează o tranzacție pentru ștergere."; return; }
        var entity = _db.Transactions.Find(SelectedTransaction.Id);
        if (entity is null) { StatusMessage = "Tranzacția nu mai există."; LoadTransactions(); return; }
        _db.Transactions.Remove(entity); _db.SaveChanges(); StatusMessage = "Tranzacția a fost ștearsă."; LoadTransactions();
    }
    [RelayCommand] private void ExportCsv()
    {
        try { var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ExpenseTrackerPro"); Directory.CreateDirectory(folder); var path = Path.Combine(folder, $"tranzactii-{DateTime.Now:yyyyMMdd-HHmmss}.csv"); var csv = new StringBuilder("Data;Descriere;Categorie;Tip;SumaRON" + Environment.NewLine); foreach (var item in Transactions) csv.Append(item.Date.ToString("yyyy-MM-dd")).Append(';').Append(Escape(item.Description)).Append(';').Append(Escape(item.Category)).Append(';').Append(Escape(item.Type)).Append(';').Append(item.Amount.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(); File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true)); StatusMessage = $"Export creat: {path}"; } catch (IOException) { StatusMessage = "Exportul nu a putut fi creat."; }
    }
    private void LoadTransactions()
    {
        var reference = SelectedDate ?? DateTime.Today; var start = new DateTime(reference.Year, reference.Month, 1); var end = start.AddMonths(1);
        var items = _db.Transactions.AsNoTracking().Where(x => x.Date >= start && x.Date < end).OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAtUtc).ToList(); Transactions.Clear(); foreach (var item in items) Transactions.Add(item);
        Income = items.Where(x => x.Type == "Venit").Sum(x => x.Amount); Expenses = items.Where(x => x.Type == "Cheltuială").Sum(x => x.Amount); Balance = Income - Expenses; RemainingBudget = MonthlyBudget - Expenses; SelectedTransaction = null;
    }
    private void SeedIfEmpty()
    {
        if (_db.Transactions.Any()) return; var now = DateTime.Today; _db.Transactions.AddRange(new TransactionItem { Id = Guid.NewGuid(), Type = "Venit", Category = "Salariu", Description = "Venit lunar", Amount = 8600m, Date = new DateTime(now.Year, now.Month, 5), CreatedAtUtc = DateTime.UtcNow }, new TransactionItem { Id = Guid.NewGuid(), Type = "Cheltuială", Category = "Utilități", Description = "Chirie și utilități", Amount = 2350m, Date = new DateTime(now.Year, now.Month, 7), CreatedAtUtc = DateTime.UtcNow }, new TransactionItem { Id = Guid.NewGuid(), Type = "Cheltuială", Category = "Alimentație", Description = "Cumpărături săptămânale", Amount = 420m, Date = new DateTime(now.Year, now.Month, 10), CreatedAtUtc = DateTime.UtcNow }); _db.SaveChanges();
    }
    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
public sealed class TransactionItem { public Guid Id { get; set; } public string Type { get; set; } = "Cheltuială"; public string Category { get; set; } = "Altele"; public string Description { get; set; } = string.Empty; public decimal Amount { get; set; } public DateTime Date { get; set; } public DateTime CreatedAtUtc { get; set; } }
public sealed class AppDbContext : DbContext { private readonly string _path; public DbSet<TransactionItem> Transactions => Set<TransactionItem>(); public AppDbContext(string path) => _path = path; protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite($"Data Source={_path}"); protected override void OnModelCreating(ModelBuilder modelBuilder) { var transaction = modelBuilder.Entity<TransactionItem>(); transaction.HasKey(x => x.Id); transaction.Property(x => x.Type).HasMaxLength(20).IsRequired(); transaction.Property(x => x.Category).HasMaxLength(60).IsRequired(); transaction.Property(x => x.Description).HasMaxLength(120).IsRequired(); transaction.Property(x => x.Amount).HasPrecision(18, 2); transaction.HasIndex(x => x.Date); } }
