# ExpenseTracker Pro

Aplicație desktop Windows pentru urmărirea bugetului personal, construită cu C#, WPF, MVVM și SQLite.
Datele rămân local, pe calculatorul tău.

## Structură

```
ExpenseTrackerPro.sln
src/
  ExpenseTrackerPro.Core/      net8.0          modele, DbContext, migrări EF, servicii, parsare sume, import/export CSV, backup
  ExpenseTrackerPro/           net8.0-windows  aplicația WPF (Views, ViewModels, IDialogService, compoziția serviciilor în App.xaml.cs)
tests/
  ExpenseTrackerPro.Tests/     net8.0          teste xUnit pe SQLite in-memory
.github/workflows/ci.yml       build + teste pe windows-latest
```

Logica de business (validare, totaluri lunare, buget, filtrare, CSV, backup, migrări) se află în `ExpenseTrackerPro.Core`,
fără dependență de WPF, deci poate fi testată și pe Linux/macOS.

## Cerințe

- Windows 10/11 pentru a rula aplicația
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Rulare

```powershell
dotnet restore
dotnet run --project src/ExpenseTrackerPro
```

Baza de date locală se creează automat în `%LOCALAPPDATA%\ExpenseTrackerPro\expense-tracker.db`.
La pornire, schema este adusă la zi prin migrări EF Core; bazele create de versiunile anterioare
(cu `EnsureCreated`) sunt recunoscute și păstrate, fără pierderea datelor.

## Teste

```powershell
dotnet test
```

Proiectul de teste nu depinde de WPF; pe Linux/macOS se pot compila și rula testele cu `dotnet test tests/ExpenseTrackerPro.Tests`.
`MainViewModel` nu folosește tipuri WPF, așa că fișierele din `ViewModels/` (și `IDialogService`) sunt incluse direct în proiectul de teste
și sunt testate cu un `IDialogService` fals. Un test verifică și că toate căile `{Binding ...}` din `MainWindow.xaml` există în ViewModel.

## Migrări EF Core

Instrumentul `dotnet-ef` este instalat ca local tool (`.config/dotnet-tools.json`):

```powershell
dotnet tool restore
dotnet ef migrations add NumeMigrare --project src/ExpenseTrackerPro.Core --output-dir Migrations
```

Fișierele din `src/ExpenseTrackerPro.Core/Migrations` sunt generate; nu se editează de mână.

## Publicare EXE

```powershell
dotnet publish src/ExpenseTrackerPro -c Release -r win-x64 --self-contained true
```

## Funcții incluse

- Venituri și cheltuieli salvate local în SQLite
- Categorii separate pentru venituri și cheltuieli (nu poți salva „Salariu” ca și cheltuială)
- Navigare între luni cu ◀ / ▶ și buton „Azi” pentru luna curentă
- Dashboard lunar: venituri, cheltuieli, sold și buget rămas, cu bară de progres pentru buget; bugetul rămas și soldul devin roșii când sunt negative
- Buget lunar configurabil din interfață (implicit 7500 RON, păstrat în setări)
- Editarea unei tranzacții: selectezi rândul și apeși „Editează” (sau dublu-click); formularul trece în modul „Editează tranzacția”, iar „Renunță” anulează. Schimbarea lunii sau ștergerea rândului editat anulează editarea. O categorie salvată de versiuni vechi, care nu mai este validă pentru tipul tranzacției, rămâne vizibilă în listă, dar trebuie înlocuită înainte de salvare
- Ștergere cu confirmare
- Căutare în descriere (fără să conteze literele mari/mici sau diacriticele, ex. `sedinta` găsește „Ședință”) și filtru pe categorie; filtrul afectează doar lista, nu și totalurile lunii
- Grafic „Cheltuieli pe categorii” (bare proporționale, fără pachete externe)
- Sume acceptate cu virgulă sau punct ca separator zecimal (`12,50` sau `12.50`), maximum 2 zecimale; valorile cu ambele separatoare (ex. `1.234,56`) sunt respinse ca ambigue
- Formatare ro-RO pentru sume și date
- Validare date de intrare, cu mesaje în română
- Export CSV al întregii luni selectate în `Documente\ExpenseTrackerPro\tranzactii-AAAA-LL.csv` (UTF-8 cu BOM, câmpuri între ghilimele, protecție împotriva formulelor injectate în Excel)
- Import CSV din fișiere în același format (antet `Data;Descriere;Categorie;Tip;SumaRON`, UTF-8 cu sau fără BOM). Rândurile invalide sunt raportate cu numărul liniei, iar duplicatele (aceeași dată, tip, categorie, descriere și sumă ca o tranzacție existentă) sunt ignorate, deci același fișier poate fi importat de mai multe ori fără efect. Rezultat: „Importate X, ignorate Y duplicate, Z erori.”
- Backup și restaurare a bazei de date (butoanele „Backup” și „Restaurează”). Backup-ul folosește API-ul online de backup SQLite, deci funcționează cât timp aplicația este deschisă. Restaurarea verifică fișierul (bază SQLite cu tabelul `Transactions`), cere confirmare, salvează datele curente în `expense-tracker.before-restore.db` (în același folder cu baza de date) și aplică migrările dacă backup-ul este mai vechi

Nu salva fișierul `.db` în GitHub, deoarece poate conține date financiare personale.
