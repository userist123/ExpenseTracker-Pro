# ExpenseTracker Pro

Aplicație desktop Windows, offline-first, pentru importul și analiza extraselor bancare personale.

## Cerințe

- Windows 10/11
- .NET 8 SDK

## Pornire

```powershell
cd ExpenseTrackerPro
dotnet restore
dotnet run
```

## Import extras

1. Deschide fila **Import extras**.
2. Alege un fișier `.csv` sau `.xlsx` de maximum 15 MB.
3. Aplicația detectează coloane uzuale: dată, descriere, debit, credit sau sumă.
4. Tranzacțiile duplicate sunt ignorate prin compararea datei, sumei, tipului și descrierii.
5. Extrasul nu este încărcat în cloud; este procesat local și tranzacțiile sunt salvate în SQLite.

Pentru rezultate consistente, exportă extrasul cu antete de tipul `Data`, `Descriere`, `Debit`, `Credit` sau `Sumă`.

## Analiză

Fila **Analiză** calculează:

- cheltuieli discreționare pentru abonamente, cumpărături și distracție;
- top categorii și comercianți repetați;
- abonamente detectate pentru comercianți cunoscuți;
- tranzacții neobișnuit de mari;
- proiecția cheltuielilor până la finalul lunii și riscul de depășire a bugetului.

Recomandările sunt euristice și explicabile; nu sunt consultanță financiară și nu etichetează automat o plată ca inutilă.

## Stocare locală

Baza de date este creată în `%LOCALAPPDATA%\ExpenseTrackerPro\expense-tracker.db`. Nu încărca această bază sau extrase bancare în repository.
