# ExpenseTracker Pro

Aplicație desktop Windows pentru urmărirea bugetului personal, construită cu C#, WPF, MVVM și SQLite.

## Cerințe

- Windows 10/11
- .NET 8 SDK

## Rulare

```powershell
cd ExpenseTrackerPro
dotnet restore
dotnet run
```

Baza de date locală se creează automat în `%LOCALAPPDATA%\ExpenseTrackerPro\expense-tracker.db`.

## Publicare EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

## Funcții incluse

- Venituri și cheltuieli salvate local în SQLite
- Dashboard lunar
- Validare date de intrare
- Ștergerea unei tranzacții selectate
- Export CSV în Documents\ExpenseTrackerPro

Nu salva fișierul `.db` în GitHub, deoarece poate conține date financiare personale.
