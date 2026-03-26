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
- [x] S14 — Start/cancel session backend
- [x] S15 — Otázkový engine a timeout progression
- [x] S16 — SignalR session groups a eventy
- [x] S17 — Team UI: join, waiting room, question screen
- [x] S18 — Answer submit backend
- [x] S19 — Výsledky, ranking a correct answers
- [x] S20 — Hardening a bezpečnostní minimum
- [ ] S21 — Testy a release readiness

## Naposledy dokončeno
- S20 — Hardening a bezpečnostní minimum (ověřeno build + testy).

## Aktuální poznámky
- V `QuizApp.Server/Program.cs` je doplněn rate limiting middleware s politikami `JoinPerIp` (10/min), `SubmitPerTeam` (20/min) a `OrganizerMutations` (10/min), plus `UseForwardedHeaders` a `UseHsts` mimo development pro TLS-ready provoz.
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` a `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` jsou mutační endpointy napojeny na odpovídající rate limit policy.
- V `QuizApp.Server/Application/Common/TextInputSanitizer.cs` vznikla centralizovaná sanitizace textových vstupů; je použita v `QuizManagementService` (název kvízu) a `SessionParticipationService` (název týmu) včetně délkových validací dle EF limitů.
- Klientské SignalR obrazovky (`OrganizerWaitingRoom`, `TeamWaitingRoom`, `TeamQuestion`) používají `ReconnectWithinSixtySecondsPolicy` pro reconnect okno do 60 sekund.
- V testech přibyly scénáře pro sanitizaci a délkové validace (`QuizManagementServiceTests`, `SessionParticipationServiceTests`).
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
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` přibyly operace `StartSessionAsync` a `CancelSessionAsync` s autorizací (`X-Organizer-Token` nebo `X-Quiz-Password`), validací přechodů (`WAITING -> RUNNING`, zákaz mutace terminálních stavů), podmínkou min. 1 týmu pro start, concurrency ošetřením a audit logy `SESSION_STARTED` / `SESSION_CANCELLED`.
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` přibyly endpointy `POST /api/sessions/{sessionId}/start` a `POST /api/sessions/{sessionId}/cancel`; cancel vyžaduje payload `CancelSessionRequest` s explicitním potvrzením.
- V `QuizApp.Shared/Contracts/SessionContracts.cs` přibyl kontrakt `CancelSessionRequest` pro potvrzení zrušení session.
- `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` nově umožňuje session spustit/zrušit, volá nové endpointy a před rušením vyžaduje potvrzení přes dialog.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy pro S14 (start bez týmu, start s týmem, cancel bez potvrzení, cancel RUNNING session, zákaz mutace terminálního stavu).
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` nyní `StartSessionAsync` nastavuje první otázku (`CurrentQuestionIndex`, `CurrentQuestionStartedAtUtc`, `QuestionDeadlineUtc`) a přibyla operace `ProgressDueSessionsAsync` pro serverový přechod na další otázku po timeoutu i finalizaci session po poslední otázce.
- V `QuizApp.Server/Application/Sessions/SessionProgressionBackgroundService.cs` vznikla hostovaná služba s periodickým zpracováním běžících session bez sekundových event ticků do klienta.
- V `QuizApp.Server/Program.cs` je registrována background služba pro timeout progression.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy S15 pro posun na další otázku po timeoutu a přechod do `FINISHED` po timeoutu poslední otázky.
- V `QuizApp.Server/Application/Sessions/SessionHub.cs` vznikl SignalR hub s metodami pro subscribe/unsubscribe do session-specific groups (`session:{sessionId}`).
- V `QuizApp.Server/Application/Sessions/SessionRealtimePublisher.cs` vznikla realtime publikační služba, která emituje eventy podle `RealtimeEventName` do konkrétní session group.
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` jsou nyní emitovány eventy `team.joined`, `session.started`, `question.changed`, `session.cancelled`, `session.finished`, `results.ready` při join/start/cancel/progression operacích.
- V `QuizApp.Server/Program.cs` je namapován endpoint hubu `MapHub<SessionHub>("/hubs/sessions")` a registrován `ISessionRealtimePublisher`.
- `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` nově navazuje SignalR připojení s automatic reconnect, subscribuje session group a při realtime eventech obnovuje snapshot přes REST.
- V `QuizApp.Client/QuizApp.Client.csproj` byl přidán balíček `Microsoft.AspNetCore.SignalR.Client` pro WASM klienta.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy na publikaci realtime eventů (`team.joined`, `session.finished`, `results.ready`).
- V `QuizApp.Client/Team/TeamSessionLocalStore.cs` vzniklo lokální úložiště týmové identity (`sessionId + teamId + teamName + TeamReconnectToken`) a lokálně uzamčených odpovědí per otázka (`sessionId + questionId + OptionKey`) přes `localStorage`.
- `QuizApp.Client/Pages/TeamJoin.razor` nyní obsahuje funkční join formulář, volání `POST /api/sessions/join`, uložení identity týmu a přesměrování do týmové čekárny.
- `QuizApp.Client/Pages/TeamWaitingRoom.razor` bylo rozšířeno na funkční čekárnu týmu: načtení snapshotu přes `GET /api/sessions/{sessionId}/state?teamId={teamId}` s hlavičkou `X-Team-Reconnect-Token`, SignalR subscribe do session group a přechod na otázku po startu session.
- `QuizApp.Client/Pages/TeamQuestion.razor` bylo rozšířeno na funkční question screen: načtení aktuální otázky ze snapshotu, zobrazení variant `A/B/C/D`, lokální jednorázové uzamčení odpovědi po odeslání a realtime refresh při `question.changed` / ukončovacích eventech.
- `QuizApp.Client/Program.cs` registruje `TeamSessionLocalStore` do DI; `_Imports.razor` byl rozšířen o namespace `QuizApp.Client.Team`.
- V `QuizApp.Shared/Contracts/SessionContracts.cs` přibyly kontrakty `SubmitAnswerRequest` a `SubmitAnswerResponse` pro endpoint submitu odpovědi.
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` přibyla operace `SubmitAnswerAsync` s validací `X-Team-Reconnect-Token`, pravidel `RUNNING + aktivní otázka + deadline`, first-write-wins (`SessionId + TeamId + QuestionId`) a výpočtem `IsCorrect` + `ResponseTimeMs` při uložení `TeamAnswer`.
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` byl přidán endpoint `POST /api/sessions/{sessionId}/answers`.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy S18 (valid submit, duplicate submit, late submit, invalid reconnect token).
- V `QuizApp.Shared/Contracts/SessionContracts.cs` přibyly kontrakty `SessionResultsResponse`, `SessionResultDto`, `CorrectAnswersResponse`, `CorrectAnswerDto` pro výsledky a správné odpovědi.
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` přibyly operace `GetSessionResultsAsync` (dual-auth: tým nebo organizátor), `GetCorrectAnswersAsync` (pouze organizátor) a privátní `ComputeSessionResultsAsync` (ranking: skóre DESC, celkový čas správných odpovědí ASC, sdílený rank při shodě).
- `ProgressDueSessionsAsync` nyní při finalizaci session (po timeoutu poslední otázky) automaticky počítá a ukládá `SessionResult` entity.
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` přibyly endpointy `GET /api/sessions/{sessionId}/results` a `GET /api/sessions/{sessionId}/correct-answers`.
- `QuizApp.Client/Pages/TeamQuestion.razor` nyní volá backend submit (`POST /api/sessions/{sessionId}/answers` s `X-Team-Reconnect-Token`) místo pouze lokálního uzamčení z S17; rozlišuje FINISHED (přechod na výsledky) a CANCELLED (přechod na hlavní stránku).
- `QuizApp.Client/Pages/SessionResults.razor` bylo nahrazeno funkční stránkou výsledků pro tým: načtení identity z `TeamSessionLocalStore`, volání `GET /api/sessions/{sessionId}/results?teamId={teamId}` s `X-Team-Reconnect-Token`, ranked tabulka se zvýrazněním vlastního týmu.
- `QuizApp.Client/Pages/OrganizerSessionResults.razor` vznikla stránka výsledků pro organizátora: načtení tokenu, volání endpointů pro výsledky a správné odpovědi, tabulka rankingu a přehled správných odpovědí s označením správné varianty.
- `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` nově obsahuje odkaz „Zobrazit výsledky a správné odpovědi" při stavu FINISHED.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibylo 9 nových testů S19 (výpočet výsledků, ranked výsledky pro tým/organizátora, chybějící auth, pre-FINISHED odmítnutí, správné odpovědi, tie-break).
- Další krok je `S20`.

## Rizika / dluh
- Ověření `database update` proti lokálnímu PostgreSQL v tomto prostředí selhalo kvůli nedostupnému `localhost:5432`; je potřeba ruční ověření na stroji s běžícím PostgreSQL.

## Poslední ověření
- Build: úspěšný (`run_build`)
- Testy: úspěšné (`run_tests` pro projekt `QuizApp.Tests`; 62/62 passed)
- Ruční smoke check: neproběhl (S20 rate limit/reconnect ověření vyžaduje běžící server/klienta a interaktivní síťové podmínky)
