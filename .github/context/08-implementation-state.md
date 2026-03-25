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
- [ ] S06 — První migrace a databázový bootstrap
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
- S05 — EF Core mapování a DbContext (ověřeno 2026-03-25 UTC).

## Aktuální poznámky
- V `QuizApp.Server` byl přidán `QuizAppDbContext` v `Persistence/QuizAppDbContext.cs` a registrace DbContextu v `Program.cs` přes `UseNpgsql` s konfigurací `PostgreSqlOptions`.
- Pro všechny doménové entity jsou nakonfigurovány klíče, vztahy, povinná pole, délky stringů a indexy/unique constraints.
- Byly doplněny kritické business constraints: unikátní `JoinCode`, unikátní `SessionId + NormalizedTeamName`, unikátní `SessionId + TeamId + QuestionId`.
- U `QuizSession.ConcurrencyToken` je zapnuta optimistická concurrency (`IsConcurrencyToken`) a u `Quiz` je zapnut query filter pro logické smazání (`IsDeleted`).
- Další krok je `S06`.

## Rizika / dluh
- Zatím žádný evidovaný technický dluh.

## Poslední ověření
- Build: úspěšný (`run_build`)
- Testy: úspěšné (`run_tests` pro projekt `QuizApp.Tests`)
- Ruční smoke check: neproběhl (vyžaduje ruční kontrolu v IDE)
