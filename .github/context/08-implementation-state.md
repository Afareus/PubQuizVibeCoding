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
- [x] S09 — REST endpointy pro správu kvízů
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
- S09 — REST endpointy pro správu kvízů (ověřeno 2026-03-26 UTC).

## Aktuální poznámky
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` vznikly endpointy `POST /api/quizzes`, `POST /api/quizzes/{quizId}/import-csv`, `GET /api/quizzes/{quizId}` a `DELETE /api/quizzes/{quizId}` s mapováním `ApiErrorCode -> HTTP status`.
- `Program.cs` nyní mapuje `app.MapQuizManagementEndpoints()`.
- `QuizManagementService` byl rozšířen o `GetQuizDetailAsync` a `DeleteQuizAsync`; delete kontroluje aktivní session (`WAITING`/`RUNNING`) a zapisuje audit `QUIZ_DELETED`.
- Organizátorské operace (`import`, `detail`, `delete`) podporují autentizaci přes `X-Organizer-Token` nebo `X-Quiz-Password`; samotné smazání stále vyžaduje správné mazací heslo.
- V `QuizApp.Shared/Contracts/QuizContracts.cs` byly doplněny DTO pro detail kvízu (`QuizDetailResponse`, `QuizDetailQuestionDto`, `QuizDetailQuestionOptionDto`).
- V `QuizApp.Tests/QuizManagementServiceTests.cs` byly rozšířeny testy o scénáře detailu, mazání, aktivní session a autentizace přes heslo.
- Další krok je `S10`.

## Rizika / dluh
- Ověření `database update` proti lokálnímu PostgreSQL v tomto prostředí selhalo kvůli nedostupnému `localhost:5432`; je potřeba ruční ověření na stroji s běžícím PostgreSQL.

## Poslední ověření
- Build: úspěšný (`run_build`)
- Testy: úspěšné (`run_tests` pro projekt `QuizApp.Tests`; 26/26 passed)
- Ruční smoke check: neproběhl (REST endpointy vyžadují ruční ověření přes Swagger/Postman na běžícím serveru)
