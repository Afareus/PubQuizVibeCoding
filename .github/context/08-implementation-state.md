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
- [x] S07 — CSV kontrakt, parser a validační report
- [x] S08 — Služba pro založení kvízu a import otázek
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
- S08 — Služba pro založení kvízu a import otázek (ověřeno 2026-03-26 UTC).

## Aktuální poznámky
- V `QuizApp.Server/Application/Quizzes` byla přidána služba `QuizManagementService` (`IQuizManagementService`) pro vytvoření kvízu a jednorázový import CSV otázek.
- `CreateQuizAsync` generuje organizer token s 256bit entropií, ukládá pouze hash tokenu, hashuje mazací heslo přes PBKDF2 a vrací token pouze v `CreateQuizResponse`.
- `ImportQuizCsvAsync` validuje organizer token constant-time porovnáním hashů, povolí import jen do prázdného kvízu, mapuje CSV na `Question` + `QuestionOption` a vrací validační report parseru.
- Audit log nyní pokrývá akce `QUIZ_CREATED` a `QUIZ_IMPORTED`.
- V `Program.cs` byla doplněna DI registrace pro `IQuizCsvParser` a `IQuizManagementService`.
- V `QuizApp.Tests` byly přidány testy `QuizManagementServiceTests` (create/import/token auth) a balíček `Microsoft.EntityFrameworkCore.InMemory`.
- Další krok je `S09`.

## Rizika / dluh
- Ověření `database update` proti lokálnímu PostgreSQL v tomto prostředí selhalo kvůli nedostupnému `localhost:5432`; je potřeba ruční ověření na stroji s běžícím PostgreSQL.

## Poslední ověření
- Build: úspěšný (`run_build`)
- Testy: úspěšné (`run_tests` pro projekt `QuizApp.Tests`; 21/21 passed včetně `QuizManagementServiceTests`)
- Ruční smoke check: neproběhl (CSV import UI/API bude ověřen až v navazujících krocích)
