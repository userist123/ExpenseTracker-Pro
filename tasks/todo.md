# ExpenseTracker Pro — îmbunătățiri (punctele 1–4)

Execuție: secvențial (Faza A apoi Faza B), cu review independent înainte de fiecare push.

## Arhitectură țintă

```
ExpenseTrackerPro.sln
src/ExpenseTrackerPro.Core/      net8.0  — modele, DbContext, migrări, servicii, parsare (testabil pe Linux)
src/ExpenseTrackerPro/           net8.0-windows WPF — Views, ViewModels, IDialogService
tests/ExpenseTrackerPro.Tests/   net8.0  xUnit + SQLite in-memory
.github/workflows/ci.yml         windows-latest: build + test
```

## Faza A — fundație (punctele 1, 2, 4)

- [x] Repo: șterge `22`, README în rădăcină, `.sln`, structură `src/` + `tests/`
- [x] Core: `TransactionType` enum + ValueConverter compatibil cu datele existente ("Venit"/"Cheltuială")
- [x] Core: categorii per tip (venit vs. cheltuială)
- [x] Core: `AmountParser` — fără ambiguitate punct/virgulă (bug `12.50` → 1250)
- [x] Core: `TransactionService` cu `IDbContextFactory`, totaluri lunare
- [x] Core: migrări EF (InitialCreate = schema actuală) + baseline pentru baze create cu `EnsureCreated`
- [x] Core: migrare `AddSettings` — buget lunar configurabil
- [x] Core: export CSV prinde `UnauthorizedAccessException`; protecție formula-injection
- [x] Fără date demo în baza reală
- [x] App: cultură `ro-RO` globală pentru formatare
- [x] App: categorie filtrată după tip; diacritice corecte; cod/XAML formatat
- [x] Teste xUnit: parsare, totaluri, CRUD, baseline migrare, CSV
- [x] CI GitHub Actions (windows-latest)
- [x] EF Core → ultimul patch 8.0.x

## Faza B — funcții (punctul 3)

- [x] Editare tranzacție
- [x] Confirmare la ștergere (prin `IDialogService`)
- [x] Navigare lună ◀ / ▶
- [x] Bară progres buget + roșu la sold negativ / buget depășit; setare buget din UI
- [x] Căutare + filtru categorie
- [x] Grafic cheltuieli pe categorii (fără dependență nouă)
- [x] Import CSV
- [x] Backup / restore `.db`
- [x] Teste pentru logica nouă

## Review

- Build Release (`--no-incremental`): 0 erori, 0 avertismente. Teste: 259/259 trec (verificat independent, nu doar din raport).
- CI GitHub Actions (windows-latest) verde pe Faza A; Faza B verificată după push.
- Review manual: `AmountParser` (regresia `12.50`), baseline `EnsureCreated` → migrări, pornire `App`, `BackupService` (validare, copie `before-restore`, rollback).
- Test permanent `XamlBindingTests` verifică că toate binding-urile din `MainWindow.xaml` există în ViewModel.
- **Neverificat:** UI-ul WPF rulat efectiv (aspect la 960×640, stiluri `ProgressBar`, `DataTrigger`-e). Trebuie pornit o dată pe Windows.
- Butoanele din `MessageBox` (Da/Nu) urmează limba Windows-ului, nu sunt forțate în română.
