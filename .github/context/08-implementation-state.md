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
- [x] S10 — Organizátorské UI pro kvízy
- [x] S11 — Session create backend a join code
- [x] S12 — Team join backend a reconnect identita
- [x] S13 — Organizátorský waiting room a session create UI
- [ ] S14 — Start/cancel session backend
- [ ] S15 — Otázkový engine a timeout progression
- [ ] S16 — SignalR session groups a eventy
- [ ] S17 — Team UI: join, waiting room, question screen
- [ ] S18 — Answer submit backend
- [ ] S19 — Výsledky, ranking a correct answers
- [ ] S20 — Hardening a bezpečnostní minimum
- [ ] S21 — Testy a release readiness

## Naposledy dokončeno
- S13 — Organizátorský waiting room a session create UI (ověřeno 2026-03-26 UTC).

## Aktuální poznámky
- V `QuizApp.Client/Organizer/OrganizerQuizLocalStore.cs` vzniklo ukládání lokálního seznamu organizátorských kvízů (`quizId + QuizOrganizerToken`) přes `localStorage`.
- `QuizApp.Client/Pages/OrganizerDashboard.razor` nyní obsahuje funkční formulář „Nový kvíz“, volání `POST /api/quizzes`, zobrazení jednorázového tokenu a lokální dashboard uložených kvízů.
- `QuizApp.Client/Pages/OrganizerQuizDetail.razor` bylo rozšířeno na funkční UI pro načtení detailu (`GET`), CSV import (`POST /import-csv`) včetně validačního reportu a smazání (`DELETE`) s heslem.
- Organizátorské UI podporuje autentizaci přes uložený `X-Organizer-Token` i manuálně zadané `X-Quiz-Password`.
- `QuizApp.Client/Program.cs` registruje `OrganizerQuizLocalStore` do DI; `_Imports.razor` byl rozšířen o `QuizApp.Shared.Contracts` a `QuizApp.Shared.Enums`.
- Pro opravu volání API z WASM klienta mimo server origin byl doplněn `QuizApp.Client/wwwroot/appsettings.json` s `ApiBaseUrl` a klientský `HttpClient` používá tuto konfiguraci.
- V `QuizApp.Server/Program.cs` je doplněna CORS policy `ClientOrigins` pro localhost originy, aby UI volání (`POST /api/quizzes` a další) fungovala i při běhu klienta a serveru na různých portech.
- V `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` přibyla operace `CreateSessionAsync`, která při autorizaci (`X-Organizer-Token` nebo `X-Quiz-Password`) založí session jen nad kvízem s otázkami, vygeneruje unikátní join code, nastaví stav `WAITING` a zapisuje audit `SESSION_CREATED`.
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` byl přidán endpoint `POST /api/quizzes/{quizId}/sessions`.
- V `QuizApp.Tests/QuizManagementServiceTests.cs` přibyly testy pro `CreateSessionAsync` (bez otázek => konflikt, autorizace heslem, opakované vytvoření session).
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` vznikly týmové endpointy `POST /api/sessions/join` a `GET /api/sessions/{sessionId}/state?teamId={teamId}` s autorizací přes `X-Team-Reconnect-Token` pro state snapshot.
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` byla doplněna join logika: validace join code, stav `WAITING`, unikátní název týmu v session, limit 20 týmů, generování jednorázového `TeamReconnectToken` a ukládání pouze jeho hashe.
- Session state snapshot ověřuje `TeamReconnectToken` constant-time porovnáním hashů, aktualizuje `LastSeenAtUtc` a vrací stav session + seznam týmů + aktuální otázku (pokud existuje).
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy pro pravidla S12 (valid join, duplicate team name, invalid join code, valid/invalid reconnect token).
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` přibyl organizátorský endpoint `GET /api/sessions/{sessionId}` a v `SessionParticipationService` nová operace `GetOrganizerSessionStateAsync` s autentizací přes `X-Organizer-Token` nebo `X-Quiz-Password`.
- V `QuizApp.Shared/Contracts/SessionContracts.cs` vznikl kontrakt `OrganizerSessionSnapshotResponse` pro waiting room snapshot (join code, stav session, seznam týmů).
- `QuizApp.Client/Pages/OrganizerQuizDetail.razor` nově umožňuje vytvořit session (`POST /api/quizzes/{quizId}/sessions`), zobrazit `SessionId + JoinCode` a přejít do čekárny.
- `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` je nahrazeno funkční obrazovkou čekárny: načtení snapshotu přes `GET /api/sessions/{sessionId}`, podpora lokálního tokenu podle `quizId`, zobrazení stavu session a připojených týmů.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy organizátorského snapshotu (validní heslo, chybějící autentizace).
- Další krok je `S14`.

## Rizika / dluh
- Ověření `database update` proti lokálnímu PostgreSQL v tomto prostředí selhalo kvůli nedostupnému `localhost:5432`; je potřeba ruční ověření na stroji s běžícím PostgreSQL.

## Poslední ověření
- Build: úspěšný (`run_build`)
- Testy: úspěšné (`run_tests` pro projekt `QuizApp.Tests`; 36/36 passed)
- Ruční smoke check: neproběhl (nové UI/endpointy S13 vyžadují ruční ověření v běžícím serveru/klientovi)
