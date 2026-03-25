# Stav implementace

Tento soubor je provozní paměť repozitáře.
Po každém kroku jej aktualizuj.

## Jak aktualizovat
- Označ dokončený krok `[x]`.
- U dalšího kroku ponech `[ ]`.
- Aktualizuj sekci „Naposledy dokončeno“.
- Aktualizuj sekci „Aktuální poznámky“.
- Aktualizuj sekci „Rizika / dluh“ jen pokud skutečně něco zůstává.

## Roadmap status
- [x] S00 — Bootstrap repozitáře a solution
- [x] S01 — Základ hostingu a konfigurace serveru
- [x] S02 — Základ klienta a routingu
- [x] S03 — Sdílené kontrakty a enumy
- [x] S04 — Entitní model domény
- [x] S05 — EF Core mapování a DbContext
- [x] S06 — První migrace a databázový bootstrap
- [ ] S07 — CSV kontrakt, parser a validační report
- [ ] S08 — Služba pro založení kvízu a import otázek
- [ ] S09 — REST endpointy pro správu kvízů
- [ ] S10 — Organizátorské UI pro kvízy
- [ ] S11 — Session create backend a join code
- [ ] S12 — Team join backend a reconnect identita
- [ ] S13 — Organizátorský waiting room a session create UI
- [ ] S14 — Start/cancel session backend
- [ ] S15 — Otázkový engine a timeout progression
- [ ] S16 — SignalR session groups a eventy
- [ ] S17 — Team UI: join, waiting room, question screen
- [ ] S18 — Answer submit backend
- [ ] S19 — Výsledky, ranking a correct answers
- [ ] S20 — Hardening a bezpečnostní minimum
- [ ] S21 — Testy a release readiness

## Naposledy dokončeno
- S06 — První migrace a databázový bootstrap (ověřeno 2026-03-25 UTC).

## Aktuální poznámky
- V `QuizApp.Server/Persistence/Migrations` byla vytvořena první EF Core migrace `InitialCreate` včetně snapshotu modelu.
- V `Program.cs` je doplněn databázový bootstrap přes `Database.MigrateAsync()` při startu aplikace.
- Do `QuizApp.Server.csproj` byl přidán `Microsoft.EntityFrameworkCore.Design` pro design-time migrace.
- Do kořene repozitáře byl přidán `dotnet-tools.json` s lokálním nástrojem `dotnet-ef` (`8.0.12`) pro konzistentní spouštění migrací.
- Další krok je `S07`.

## Rizika / dluh
- Ověření `database update` proti lokálnímu PostgreSQL v tomto prostředí selhalo kvůli nedostupnému `localhost:5432`; je potřeba ruční ověření na stroji s běžícím PostgreSQL.

## Poslední ověření
- Build: úspěšný (`run_build`)
- Testy: úspěšné (`run_tests` pro projekt `QuizApp.Tests`)
- EF migrace: vytvoření úspěšné (`dotnet dotnet-ef migrations add InitialCreate`)
- EF database update: neúspěšné v tomto prostředí (`Failed to connect to 127.0.0.1:5432`)
- Ruční smoke check: neproběhl (vyžaduje ruční kontrolu v IDE)
