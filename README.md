# ExpenseTracker Pro

Aplicație desktop Windows pentru urmărirea bugetului personal, construită cu C#, WPF, MVVM și SQLite.
Datele rămân local, pe calculatorul tău.

## Structură

```
ExpenseTrackerPro.sln
src/
  ExpenseTrackerPro.Core/      net8.0          modele, DbContext, migrări EF, servicii, parsare sume, export CSV
  ExpenseTrackerPro/           net8.0-windows  aplicația WPF (Views, ViewModels, compoziția serviciilor în App.xaml.cs)
tests/
  ExpenseTrackerPro.Tests/     net8.0          teste xUnit pe SQLite in-memory
.github/workflows/ci.yml       build + teste pe windows-latest
```

Logica de business (validare, totaluri lunare, buget, CSV, migrări) se află în `ExpenseTrackerPro.Core`,
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
- Dashboard lunar: venituri, cheltuieli, sold și buget rămas (buget lunar implicit 7500 RON, păstrat în setări)
- Sume acceptate cu virgulă sau punct ca separator zecimal (`12,50` sau `12.50`), maximum 2 zecimale; valorile cu ambele separatoare (ex. `1.234,56`) sunt respinse ca ambigue
- Formatare ro-RO pentru sume și date
- Validare date de intrare, cu mesaje în română
- Ștergerea unei tranzacții selectate
- Export CSV al lunii selectate în `Documente\ExpenseTrackerPro\tranzactii-AAAA-LL.csv` (UTF-8 cu BOM, câmpuri între ghilimele, protecție împotriva formulelor injectate în Excel)

Nu salva fișierul `.db` în GitHub, deoarece poate conține date financiare personale.
