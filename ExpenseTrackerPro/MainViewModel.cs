using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace ExpenseTrackerPro;

public partial class MainViewModel : ObservableObject
{
    private const decimal MonthlyBudget = 7500m;
    private readonly AppDbContext _db;
    private readonly Dictionary<string, (string Category, decimal Confidence)> _merchantRules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LIDL"] = ("Alimentație", .95m), ["KAUFLAND"] = ("Alimentație", .95m), ["MEGA IMAGE"] = ("Alimentație", .95m), ["CARREFOUR"] = ("Alimentație", .95m),
        ["BOLT"] = ("Transport", .95m), ["UBER"] = ("Transport", .95m), ["OMV"] = ("Transport", .90m), ["MOL"] = ("Transport", .90m),
        ["NETFLIX"] = ("Abonamente", .98m), ["SPOTIFY"] = ("Abonamente", .98m), ["YOUTUBE"] = ("Abonamente", .88m), ["DIGI"] = ("Utilități", .90m), ["ORANGE"] = ("Utilități", .90m), ["VODAFONE"] = ("Utilități", .90m),
        ["EMAG"] = ("Cumpărături", .85m), ["AMAZON"] = ("Cumpărături", .85m), ["STEAM"] = ("Distracție", .90m), ["XTB"] = ("Investiții", .95m), ["IBKR"] = ("Investiții", .95m)
    };

    public ObservableCollection<TransactionItem> Transactions { get; } = [];
    public ObservableCollection<TransactionItem> ImportPreview { get; } = [];
    public ObservableCollection<SpendingInsight> Insights { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["Salariu", "Alimentație", "Utilități", "Transport", "Sănătate", "Distracție", "Abonamente", "Cumpărături", "Investiții", "Transfer", "Neclasificat"];
    public ObservableCollection<string> TransactionTypes { get; } = ["Cheltuială", "Venit"];
    [ObservableProperty] private DateTime? selectedDate = DateTime.Today;
    [ObservableProperty] private string newType = "Cheltuială";
    [ObservableProperty] private string newCategory = "Alimentație";
    [ObservableProperty] private string newDescription = string.Empty;
    [ObservableProperty] private string newAmount = string.Empty;
    [ObservableProperty] private DateTime? newDate = DateTime.Today;
    [ObservableProperty] private string statusMessage = "Gata.";
    [ObservableProperty] private string importStatus = "Alege un extras CSV sau XLSX. Fișierul este procesat exclusiv local.";
    [ObservableProperty] private string dashboardSummary = string.Empty;
    [ObservableProperty] private string analysisHeadline = "Importă un extras pentru a genera analiza.";
    [ObservableProperty] private string analysisDetails = string.Empty;
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
        if (string.IsNullOrWhiteSpace(NewDescription) || NewDescription.Trim().Length > 180) { StatusMessage = "Introdu o descriere între 1 și 180 de caractere."; return; }
        if (!TryParseAmount(NewAmount, out var amount) || amount <= 0 || amount > 10_000_000m || NewDate is null) { StatusMessage = "Verifică suma și data."; return; }
        AddIfNew(new TransactionItem { Id = Guid.NewGuid(), Type = NewType, Category = NewCategory, Description = NewDescription.Trim(), Amount = decimal.Round(amount, 2), Date = NewDate.Value.Date, CreatedAtUtc = DateTime.UtcNow, Source = "Manual", Confidence = 1m });
        NewDescription = string.Empty; NewAmount = string.Empty; LoadTransactions(); StatusMessage = "Tranzacția a fost salvată local.";
    }

    [RelayCommand] private void ImportStatement()
    {
        var dialog = new OpenFileDialog { Filter = "Extrase CSV sau Excel|*.csv;*.xlsx|CSV|*.csv|Excel|*.xlsx", Multiselect = false, CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > 15 * 1024 * 1024) { ImportStatus = "Fișierul depășește limita de 15 MB."; return; }
            var rows = Path.GetExtension(dialog.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? ReadExcel(dialog.FileName) : ReadCsv(dialog.FileName);
            ImportPreview.Clear();
            var added = 0; var duplicates = 0; var invalid = 0;
            foreach (var row in rows)
            {
                if (row.Amount == 0 || string.IsNullOrWhiteSpace(row.Description)) { invalid++; continue; }
                var classified = Classify(row.Description, row.Type);
                row.Category = classified.Category; row.Confidence = classified.Confidence; row.Source = Path.GetFileName(dialog.FileName);
                if (Exists(row)) { duplicates++; continue; }
                AddIfNew(row); ImportPreview.Add(row); added++;
            }
            LoadTransactions();
            ImportStatus = $"Import finalizat: {added} tranzacții adăugate, {duplicates} duplicate ignorate, {invalid} rânduri invalide.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            ImportStatus = "Fișierul nu a putut fi citit. Verifică formatul și permisiunile.";
        }
    }

    [RelayCommand] private void DeleteTransaction()
    {
        if (SelectedTransaction is null) { StatusMessage = "Selectează o tranzacție pentru ștergere."; return; }
        var entity = _db.Transactions.Find(SelectedTransaction.Id); if (entity is null) { LoadTransactions(); return; }
        _db.Transactions.Remove(entity); _db.SaveChanges(); LoadTransactions(); StatusMessage = "Tranzacția a fost ștearsă.";
    }

    [RelayCommand] private void ExportCsv()
    {
        try { var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ExpenseTrackerPro"); Directory.CreateDirectory(folder); var path = Path.Combine(folder, $"tranzactii-{DateTime.Now:yyyyMMdd-HHmmss}.csv"); var csv = new StringBuilder("Data;Descriere;Categorie;Tip;SumaRON" + Environment.NewLine); foreach (var item in Transactions) csv.Append(item.Date.ToString("yyyy-MM-dd")).Append(';').Append(Escape(item.Description)).Append(';').Append(Escape(item.Category)).Append(';').Append(Escape(item.Type)).Append(';').Append(item.Amount.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(); File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true)); StatusMessage = $"Export creat: {path}"; } catch (IOException) { StatusMessage = "Exportul nu a putut fi creat."; }
    }

    private void LoadTransactions()
    {
        var reference = SelectedDate ?? DateTime.Today; var start = new DateTime(reference.Year, reference.Month, 1); var end = start.AddMonths(1);
        var items = _db.Transactions.AsNoTracking().Where(x => x.Date >= start && x.Date < end).OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAtUtc).ToList();
        Transactions.Clear(); foreach (var item in items) Transactions.Add(item);
        Income = items.Where(x => x.Type == "Venit").Sum(x => x.Amount); Expenses = items.Where(x => x.Type == "Cheltuială").Sum(x => x.Amount); Balance = Income - Expenses; RemainingBudget = MonthlyBudget - Expenses;
        BuildAnalysis(items, reference); SelectedTransaction = null;
    }

    private void BuildAnalysis(List<TransactionItem> items, DateTime reference)
    {
        Insights.Clear(); var expenses = items.Where(x => x.Type == "Cheltuială").ToList(); var discretionary = expenses.Where(x => x.Category is "Distracție" or "Abonamente" or "Cumpărături").Sum(x => x.Amount);
        var daysElapsed = Math.Max(1, Math.Min(DateTime.DaysInMonth(reference.Year, reference.Month), reference.Date > DateTime.Today && reference.Month == DateTime.Today.Month && reference.Year == DateTime.Today.Year ? DateTime.Today.Day : DateTime.DaysInMonth(reference.Year, reference.Month)));
        var projected = Expenses / daysElapsed * DateTime.DaysInMonth(reference.Year, reference.Month);
        AnalysisHeadline = $"Cheltuieli discreționare: {discretionary:N2} RON. Ritmul actual proiectează {projected:N2} RON până la finalul lunii.";
        var categories = expenses.GroupBy(x => x.Category).OrderByDescending(x => x.Sum(y => y.Amount)).Take(5).Select(x => $"{x.Key}: {x.Sum(y => y.Amount):N2} RON");
        AnalysisDetails = "Top categorii: " + (categories.Any() ? string.Join(" • ", categories) : "nu există cheltuieli în luna selectată.");
        foreach (var group in expenses.GroupBy(x => x.Category).Where(x => x.Key is "Distracție" or "Abonamente" or "Cumpărături").OrderByDescending(x => x.Sum(y => y.Amount))) Insights.Add(new SpendingInsight($"Revizuiește categoria {group.Key}", $"Ai cheltuit {group.Sum(x => x.Amount):N2} RON în {group.Count()} tranzacții. Categoria este marcată discreționară, nu obligatorie.", decimal.Round(group.Sum(x => x.Amount) * .20m, 2)));
        foreach (var group in expenses.GroupBy(x => NormalizeMerchant(x.Description)).Where(x => x.Count() >= 2 && x.Sum(y => y.Amount) >= 100).OrderByDescending(x => x.Sum(y => y.Amount)).Take(3)) Insights.Add(new SpendingInsight($"Cheltuieli repetate: {group.Key}", $"{group.Count()} plăți în luna curentă, total {group.Sum(x => x.Amount):N2} RON.", decimal.Round(group.Sum(x => x.Amount) * .15m, 2)));
        var recurring = expenses.GroupBy(x => NormalizeMerchant(x.Description)).Where(x => x.Key.Contains("NETFLIX") || x.Key.Contains("SPOTIFY") || x.Key.Contains("YOUTUBE")).ToList(); foreach (var group in recurring) Insights.Add(new SpendingInsight($"Abonament detectat: {group.Key}", $"Plăți detectate: {group.Count()}, total {group.Sum(x => x.Amount):N2} RON. Confirmă dacă serviciul este folosit.", group.Sum(x => x.Amount)));
        var average = expenses.Any() ? expenses.Average(x => x.Amount) : 0; foreach (var item in expenses.Where(x => x.Amount > average * 3 && x.Amount > 300).OrderByDescending(x => x.Amount).Take(3)) Insights.Add(new SpendingInsight("Cheltuială neobișnuit de mare", $"{item.Description}: {item.Amount:N2} RON, peste de trei ori media lunară per tranzacție ({average:N2} RON).", 0));
        if (projected > MonthlyBudget) Insights.Add(new SpendingInsight("Risc de depășire a bugetului", $"Proiecția curentă depășește bugetul de {MonthlyBudget:N2} RON cu {projected - MonthlyBudget:N2} RON.", projected - MonthlyBudget));
        if (!Insights.Any()) Insights.Add(new SpendingInsight("Date insuficiente", "Importă sau adaugă mai multe tranzacții pentru recomandări mai precise.", 0));
        DashboardSummary = $"În luna selectată ai cheltuit {Expenses:N2} RON. Din acestea, {discretionary:N2} RON sunt în categorii discreționare configurabile. Buget rămas: {RemainingBudget:N2} RON.";
    }

    private List<TransactionItem> ReadCsv(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x)).Take(10000).ToList(); if (lines.Count < 2) throw new FormatException();
        var delimiter = lines[0].Count(x => x == ';') >= lines[0].Count(x => x == ',') ? ';' : ','; var headers = Split(lines[0], delimiter).Select(Normalize).ToList(); var list = new List<TransactionItem>();
        foreach (var line in lines.Skip(1)) { var cells = Split(line, delimiter); var row = ParseRow(i => Get(cells, headers, i)); if (row is not null) list.Add(row); } return list;
    }

    private List<TransactionItem> ReadExcel(string path)
    {
        using var workbook = new XLWorkbook(path); var sheet = workbook.Worksheets.First(); var range = sheet.RangeUsed(); if (range is null || range.RowCount() < 2) throw new FormatException(); var headers = range.FirstRow().Cells().Select(c => Normalize(c.GetString())).ToList(); var list = new List<TransactionItem>();
        foreach (var row in range.RowsUsed().Skip(1).Take(10000)) { var values = row.Cells(1, headers.Count).Select(c => c.GetFormattedString()).ToList(); var item = ParseRow(i => Get(values, headers, i)); if (item is not null) list.Add(item); } return list;
    }

    private TransactionItem? ParseRow(Func<string[], string> value)
    {
        var dateText = value(["data", "date", "transactiondate"]); var description = value(["descriere", "description", "detalii", "merchant", "beneficiar"]); var debit = value(["debit", "iesire", "outgoing"]); var credit = value(["credit", "intrare", "incoming"]); var amountText = value(["suma", "amount", "valoare"]);
        if (!TryParseDate(dateText, out var date) || string.IsNullOrWhiteSpace(description)) return null;
        decimal amount; string type;
        if (TryParseAmount(debit, out var debitAmount) && debitAmount > 0) { amount = debitAmount; type = "Cheltuială"; }
        else if (TryParseAmount(credit, out var creditAmount) && creditAmount > 0) { amount = creditAmount; type = "Venit"; }
        else if (TryParseAmount(amountText, out var signedAmount)) { amount = Math.Abs(signedAmount); type = signedAmount < 0 ? "Cheltuială" : "Venit"; }
        else return null;
        return new TransactionItem { Id = Guid.NewGuid(), Date = date, Description = description.Trim()[..Math.Min(180, description.Trim().Length)], Amount = decimal.Round(amount, 2), Type = type, Category = "Neclasificat", CreatedAtUtc = DateTime.UtcNow };
    }

    private bool Exists(TransactionItem item) => _db.Transactions.Any(x => x.Date == item.Date && x.Amount == item.Amount && x.Type == item.Type && x.Description == item.Description);
    private void AddIfNew(TransactionItem item) { if (!Exists(item)) { _db.Transactions.Add(item); _db.SaveChanges(); } }
    private (string Category, decimal Confidence) Classify(string description, string type) { if (type == "Venit") return ("Salariu", .55m); foreach (var rule in _merchantRules) if (description.Contains(rule.Key, StringComparison.OrdinalIgnoreCase)) return rule.Value; return ("Neclasificat", .25m); }
    private static string NormalizeMerchant(string value) => new string(value.ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();
    private static string Normalize(string value) => new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    private static string Get(IReadOnlyList<string> cells, IReadOnlyList<string> headers, string[] aliases) { var index = headers.Select((header, i) => (header, i)).FirstOrDefault(x => aliases.Any(a => x.header.Contains(a))).i; return index >= 0 && index < cells.Count ? cells[index] : string.Empty; }
    private static bool TryParseDate(string input, out DateTime date) => DateTime.TryParse(input, CultureInfo.GetCultureInfo("ro-RO"), DateTimeStyles.AllowWhiteSpaces, out date) || DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
    private static bool TryParseAmount(string input, out decimal amount) { input = input.Replace("RON", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Trim(); return decimal.TryParse(input, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.GetCultureInfo("ro-RO"), out amount) || decimal.TryParse(input, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount); }
    private static List<string> Split(string line, char delimiter) { var values = new List<string>(); var sb = new StringBuilder(); var quote = false; foreach (var c in line) { if (c == '"') quote = !quote; else if (c == delimiter && !quote) { values.Add(sb.ToString().Trim()); sb.Clear(); } else sb.Append(c); } values.Add(sb.ToString().Trim()); return values; }
    private void SeedIfEmpty() { if (_db.Transactions.Any()) return; var now = DateTime.Today; _db.Transactions.AddRange(new TransactionItem { Id = Guid.NewGuid(), Type = "Venit", Category = "Salariu", Description = "Venit lunar", Amount = 8600m, Date = new DateTime(now.Year, now.Month, 5), CreatedAtUtc = DateTime.UtcNow, Source = "Demo", Confidence = 1m }, new TransactionItem { Id = Guid.NewGuid(), Type = "Cheltuială", Category = "Utilități", Description = "Chirie și utilități", Amount = 2350m, Date = new DateTime(now.Year, now.Month, 7), CreatedAtUtc = DateTime.UtcNow, Source = "Demo", Confidence = 1m }); _db.SaveChanges(); }
    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
public sealed class SpendingInsight { public SpendingInsight(string title, string detail, decimal potentialSaving) { Title = title; Detail = detail; PotentialSaving = potentialSaving; } public string Title { get; } public string Detail { get; } public decimal PotentialSaving { get; } }
public sealed class TransactionItem { public Guid Id { get; set; } public string Type { get; set; } = "Cheltuială"; public string Category { get; set; } = "Neclasificat"; public string Description { get; set; } = string.Empty; public decimal Amount { get; set; } public DateTime Date { get; set; } public DateTime CreatedAtUtc { get; set; } public string Source { get; set; } = "Manual"; public decimal Confidence { get; set; } }
public sealed class AppDbContext : DbContext { private readonly string _path; public DbSet<TransactionItem> Transactions => Set<TransactionItem>(); public AppDbContext(string path) => _path = path; protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite($"Data Source={_path}"); protected override void OnModelCreating(ModelBuilder modelBuilder) { var x = modelBuilder.Entity<TransactionItem>(); x.HasKey(t => t.Id); x.Property(t => t.Type).HasMaxLength(20).IsRequired(); x.Property(t => t.Category).HasMaxLength(60).IsRequired(); x.Property(t => t.Description).HasMaxLength(180).IsRequired(); x.Property(t => t.Amount).HasPrecision(18, 2); x.Property(t => t.Source).HasMaxLength(260); x.HasIndex(t => new { t.Date, t.Amount, t.Type, t.Description }); } }
