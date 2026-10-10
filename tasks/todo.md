# ExpenseTracker Pro — îmbunătățiri (punctele 1–4)

Execuție: agenți Sonnet, secvențial (Faza A apoi Faza B). Review + push: Opus.

## Arhitectură țintă

```
ExpenseTrackerPro.sln
src/ExpenseTrackerPro.Core/      net8.0  — modele, DbContext, migrări, servicii, parsare (testabil pe Linux)
src/ExpenseTrackerPro/           net8.0-windows WPF — Views, ViewModels, IDialogService
tests/ExpenseTrackerPro.Tests/   net8.0  xUnit + SQLite in-memory
.github/workflows/ci.yml         windows-latest: build + test
```

## Faza A — fundație (punctele 1, 2, 4)

- [ ] Repo: șterge `22`, README în rădăcină, `.sln`, structură `src/` + `tests/`
- [ ] Core: `TransactionType` enum + ValueConverter compatibil cu datele existente ("Venit"/"Cheltuială")
- [ ] Core: categorii per tip (venit vs. cheltuială)
- [ ] Core: `AmountParser` — fără ambiguitate punct/virgulă (bug `12.50` → 1250)
- [ ] Core: `TransactionService` cu `IDbContextFactory`, totaluri lunare
- [ ] Core: migrări EF (InitialCreate = schema actuală) + baseline pentru baze create cu `EnsureCreated`
- [ ] Core: migrare `AddSettings` — buget lunar configurabil
- [ ] Core: export CSV prinde `UnauthorizedAccessException`; protecție formula-injection
- [ ] Fără date demo în baza reală
- [ ] App: cultură `ro-RO` globală pentru formatare
- [ ] App: categorie filtrată după tip; diacritice corecte; cod/XAML formatat
- [ ] Teste xUnit: parsare, totaluri, CRUD, baseline migrare, CSV
- [ ] CI GitHub Actions (windows-latest)
- [ ] EF Core → ultimul patch 8.0.x

## Faza B — funcții (punctul 3)

- [ ] Editare tranzacție
- [ ] Confirmare la ștergere (prin `IDialogService`)
- [ ] Navigare lună ◀ / ▶
- [ ] Bară progres buget + roșu la sold negativ / buget depășit; setare buget din UI
- [ ] Căutare + filtru categorie
- [ ] Grafic cheltuieli pe categorii (fără dependență nouă)
- [ ] Import CSV
- [ ] Backup / restore `.db`
- [ ] Teste pentru logica nouă

## Review

_(completat după execuție)_
