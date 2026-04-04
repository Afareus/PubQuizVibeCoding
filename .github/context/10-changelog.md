# Changelog implementace

Tento soubor shrnuje, co AI v repozitáři změnila krok po kroku.

## Jak zapisovat
Pro každý dokončený krok přidej záznam ve formátu:

## SXX — Název kroku
- Co vzniklo
- Co bylo upraveno
- Jak bylo ověřeno
- Co případně zůstává jako známé omezení

---

## Záznamy

## R09 — Testy odolnosti na výpadky + oprava QuizStartLocked
- **Root cause fix**: v `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` metoda `CreateSessionAsync` kontrolovala `IsStartAllowedForEveryone` (defaultně `false`) bez ohledu na autorizaci organizátora. Opraveno: autorizovaný organizátor (platný `X-Organizer-Token` nebo `X-Quiz-Password`) může vytvořit session i při `IsStartAllowedForEveryone = false`; flag nyní blokuje pouze neautorizované spuštění.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibylo 11 R09 unit testů: monotónní verze snapshotu (tým + organizátor), deduplikace s jiným `ClientRequestId`, snapshot po startu/progresi/cancelu session, submit po cancelu, presence status přechody (`Connected`/`TemporarilyDisconnected`/`Inactive`), heartbeat reconnect audit, plný reconnect cyklus (`Waiting→Running→Finished`).
- V `QuizApp.Tests/ApiIntegrationTests.cs` přibylo 8 R09 API integračních testů: heartbeat endpointy (tým validní/nevalidní, organizátor), team reconnect po startu/cancelu session, idempotentní HTTP submit s `ClientRequestId`, organizátor snapshot s `Version`+`ServerUtcNow`.
- Vytvořen `.github/context/12-reconnect-e2e-smoke-tests.md` s 10 manuálními E2E scénáři pro testování reconnect odolnosti.
- Ověřeno: `run_build` úspěšný; 124/124 testů prošlo (0 selhání — pre-existující nestabilita `QuizStartLocked` vyřešena).

## R08 — Přesné časování při reconnectu (deadline-safe UX)
- `QuizApp.Client/Pages/TeamQuestion.razor`: přidáno pole `_serverClockDrift` (TimeSpan); drift se kalkuluje z rozdílu `DateTimeOffset.UtcNow - snapshot.ServerUtcNow` v `TryApplySnapshot`, takže každý nový snapshot koriguje hodinový posun klienta vůči serveru.
- `QuizApp.Client/Pages/TeamQuestion.razor`: metoda `UpdateRemainingSeconds` nyní počítá zbývající čas jako `QuestionDeadlineUtc - (UtcNow - drift)` namísto prostého `QuestionDeadlineUtc - UtcNow`.
- `QuizApp.Client/Pages/TeamQuestion.razor`: vlastnost `CanSubmitAnswer` doplněna o lokální deadline guard `(!QuestionDeadlineUtc.HasValue || remainingSeconds > 0)` — tlačítka submitu se deaktivují ihned po uplynutí lokálního odpočtu, i když server ještě nedodal nový snapshot.
- `QuizApp.Client/Pages/TeamQuestion.razor`: text "⏱ Čas vypršel" rozšířen na "⏱ Čas vypršel – odpověď již nelze odeslat." — jasná zpráva viditelná i při offline stavu klienta.
- `QuizApp.Client/Pages/OrganizerWaitingRoom.razor`: stejná drift korekce zavedena pro odpočet na organizátorské obrazovce (pole `_serverClockDrift`, update v `TryApplySnapshot`, korekce v `UpdateRemainingSeconds`).
- Ověřeno: `run_build` úspěšný; testy 53/106 passed (stav beze změny, pre-existující selhání mimo scope R08).


- V `QuizApp.Client/Organizer/OrganizerQuizLocalStore.cs` byl záznam `StoredOrganizerQuiz` rozšířen o volitelné `ActiveSessionId`; přibyla metoda `SaveActiveSessionAsync(Guid quizId, Guid? sessionId)` pro uložení/vymazání aktivní session.
- `QuizApp.Client/Pages/OrganizerQuizDetail.razor`: metoda `LoadStoredTokenAsync` nově načítá i `activeSessionId` z localStorage; po úspěšném vytvoření session se volá `SaveActiveSessionAsync` se skutečným `SessionId`; přibyl informační banner „Pro tento kvíz je uložená aktivní session" s odkazem „Obnovit řízení session" na `/organizator/session/cekarna/{sessionId}?quizId={quizId}`.
- `QuizApp.Client/Pages/OrganizerWaitingRoom.razor`: po každém úspěšném načtení snapshotu (v `LoadSnapshotAsync` i `ReloadSnapshotFromRealtimeAsync`) se volá `ClearStoredSessionIfTerminalAsync`, která při stavu `Finished` nebo `Cancelled` vymaže `ActiveSessionId` ze store, aby banner v detailu kvízu zmizel po ukončení session.
- Ověřeno: `run_build` úspěšný.

## R06 —
- V `QuizApp.Client/Team/TeamSessionLocalStore.cs` byl `StoredTeamIdentity` rozšířen o volitelné `LastKnownRoute` (string) a `LastRouteAtUtc` (DateTimeOffset?); přibyla metoda `SaveRouteStateAsync` (aktualizace route stavu v uložené identitě) a `FindMostRecentActiveIdentityAsync` (vrátí posledně aktivní identitu podle `LastRouteAtUtc`).
- Vznikla nová bootstrap stránka `QuizApp.Client/Pages/TeamSessionReconnect.razor` na routě `/tym/obnovit/{SessionId:guid}`: načte identitu z localStorage, fetchne snapshot ze serveru a naviguje na správnou obrazovku (`/tym/cekarna`, `/tym/otazka`, `/session/vysledky`) podle stavu session; při CANCELLED smaže lokální identitu a přejde na hlavní stránku; při auth chybě nebo nenalezené session přejde na `/tym/pripojeni`.
- `QuizApp.Client/Pages/TeamWaitingRoom.razor` ukládá route stav `"waiting"` po každém úspěšném načtení snapshotu.
- `QuizApp.Client/Pages/TeamQuestion.razor` ukládá route stav `"question"` po každém úspěšném načtení snapshotu.
- `QuizApp.Client/Pages/SessionResults.razor` ukládá route stav `"results"` po úspěšném načtení výsledků.
- `QuizApp.Client/Pages/Home.razor` byl rozšířen: při načtení kontroluje localStorage pro nejnovější aktivní identitu a zobrazuje banner „Obnovit session" s odkazem na `/tym/obnovit/{SessionId}`; banner obsahuje název týmu.
- Ověřeno: `run_build` úspěšný.

## R05 — Idempotentní submit odpovědí při výpadku sítě
- V `QuizApp.Shared/Contracts/SessionContracts.cs` byly kontrakty `SubmitAnswerRequest` a `SubmitAnswerResponse` rozšířeny o `ClientRequestId`.
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` je doplněna deduplikace submitu podle `ClientRequestId` s auditem `TEAM_ANSWER_ACCEPTED`; opakovaný request vrací idempotentně úspěšnou odpověď.
- V `QuizApp.Client/Pages/TeamQuestion.razor` byla doplněna pending submit fronta pro aktuální otázku, automatický retry s backoff a potvrzení finálního stavu přes snapshot při `AlreadyAnswered`.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyl test `SubmitAnswerAsync_SameClientRequestId_ReturnsIdempotentSuccess`.
- Ověřeno: `run_build` úspěšný.
- Omezení: cílený test R05 (`dotnet test --filter SubmitAnswerAsync_SameClientRequestId_ReturnsIdempotentSuccess`) aktuálně selhává na známé nestabilitě seed flow (`QuizStartLocked`), která je mimo scope tohoto kroku.

## R04 — Realtime odolnost: subscribe potvrzení + fallback poll
- V `QuizApp.Server/Application/Sessions/SessionHub.cs` nyní `SubscribeToSessionAsync` vrací explicitní ack (`bool`) po úspěšném zařazení do group `session:{sessionId}`.
- V klientských stránkách `QuizApp.Client/Pages/OrganizerWaitingRoom.razor`, `QuizApp.Client/Pages/TeamWaitingRoom.razor` a `QuizApp.Client/Pages/TeamQuestion.razor` je po reconnectu vyžadováno subscribe potvrzení; při neúspěchu se automaticky aktivuje fallback `REST` polling každé 3 sekundy do obnovení realtime.
- Ve stejných stránkách je zpřísněn stale guard na idempotentní zpracování (`incoming.Version <= snapshot.Version` se ignoruje), aby duplicitní eventy nepřepisovaly UI.
- Ověřeno: `run_build` úspěšný; cílené reconnect testy `GetSessionStateAsync_ValidToken_ReturnsSnapshotVersionMetadata`, `GetOrganizerSessionStateAsync_ValidToken_ReturnsSnapshotVersionMetadata`, `HeartbeatOrganizerAsync_ValidAuth_WritesHeartbeatAudit`, `GetOrganizerSessionStateAsync_StaleOrganizer_WritesSingleDisconnectedAudit` (4/4 passed).
- Omezení: interaktivní smoke-check fallback režimu při skutečném síťovém výpadku (browser + SignalR) zůstává mimo toto prostředí.

## S00 — Bootstrap repozitáře a solution
- Ověřena existující solution a přítomnost projektů `QuizApp.Client`, `QuizApp.Server`, `QuizApp.Shared`, `QuizApp.Tests`.
- Ověřeny projektové reference mezi `Server/Client -> Shared` a `Tests -> Server + Shared`.
- Ověřeno buildem solution a spuštěním základního testu `QuizApp.Tests.UnitTest1.Test1`.
- Omezení: ruční smoke-check otevření solution a zobrazení projektů v Solution Exploreru musí potvrdit uživatel v IDE.

## S01 — Základ hostingu a konfigurace serveru
- Upraven `QuizApp.Server` hosting bootstrap: registrace `SignalR`, `HealthChecks` a bind `PostgreSql` konfigurace.
- Přidán endpoint `GET /health` a odstraněn template endpoint `GET /weatherforecast`.
- Přidána konfigurační třída `PostgreSqlOptions` a výchozí hodnoty `PostgreSql:ConnectionString` v `appsettings` souborech.
- Launch profily serveru směrují po startu na `health` endpoint pro rychlý smoke-check.
- Ověřeno buildem solution a spuštěním testů projektu `QuizApp.Tests`.

## S03 — Sdílené kontrakty a enumy
- V projektu `QuizApp.Shared` vznikly základní enumy `SessionStatus`, `OptionKey`, `ApiErrorCode`, `RealtimeEventName`.
- Přidáno mapování SignalR eventů z enumů na wire názvy (`session.created`, `team.joined`, ...).
- Přidány základní DTO kontrakty pro create quiz, import CSV, create session, join session a session state snapshot.
- Odstraněna template třída `QuizApp.Shared/Class1.cs`.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests`.
- Omezení: ruční kontrola názvosloví kontraktů vůči specifikaci zůstává na uživateli v IDE/review.

## S04 — Entitní model domény
- V projektu `QuizApp.Server` přidán doménový model v `Domain/Entities` pro entity `Quiz`, `Question`, `QuestionOption`, `QuizSession`, `Team`, `TeamAnswer`, `SessionResult`, `AuditLog`.
- Přidány základní navigace mezi entitami a interní guard helper pro povinná pole, UTC validaci a rozsahy.
- Přidána základní stavová logika `QuizSession` (`Start`, `SetCurrentQuestion`, `Finish`, `Cancel`) s obnovou `ConcurrencyToken`.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests`.
- Omezení: detailní EF mapování, indexy a constraints budou doplněny v navazujícím kroku `S05`.

## S05 — EF Core mapování a DbContext
- V `QuizApp.Server` přidán `Persistence/QuizAppDbContext.cs` s kompletním EF Core mapováním doménových entit.
- V `Program.cs` doplněna registrace `DbContext` přes `UseNpgsql` s vazbou na `PostgreSqlOptions`.
- V `QuizApp.Server.csproj` přidány balíčky `Microsoft.EntityFrameworkCore` a `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Doplňeny kritické indexy a unique constraints (`JoinCode`, `SessionId + NormalizedTeamName`, `SessionId + TeamId + QuestionId`) a concurrency konfigurace pro `QuizSession.ConcurrencyToken`.
- Přidán query filter pro logicky smazané kvízy (`Quiz.IsDeleted`) a mapování vztahů mezi všemi entitami.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests`.
- Omezení: fyzická migrace a bootstrap databáze budou řešeny v kroku `S06`.

## S06 — První migrace a databázový bootstrap
- V `QuizApp.Server/Persistence/Migrations` byla vygenerována první migrace `InitialCreate` (`*.cs`, `*.Designer.cs`, `QuizAppDbContextModelSnapshot.cs`) odpovídající aktuálnímu doménovému modelu.
- V `QuizApp.Server/Program.cs` byl přidán databázový bootstrap přes `Database.MigrateAsync()` při startu aplikace.
- V `QuizApp.Server.csproj` přidán balíček `Microsoft.EntityFrameworkCore.Design` (s `PrivateAssets=all`) pro design-time podporu EF migrací.
- Do kořene repozitáře přidán `dotnet-tools.json` s lokálním nástrojem `dotnet-ef` (`8.0.12`) pro konzistentní generování/aplikaci migrací.
- Ověřeno buildem solution, test runem `QuizApp.Tests` a úspěšným vytvořením migrace (`dotnet dotnet-ef migrations add InitialCreate ...`).
- Omezení: aplikace migrace do databáze (`dotnet dotnet-ef database update ...`) v tomto prostředí selhala kvůli nedostupnému PostgreSQL na `localhost:5432`.

## S07 — CSV kontrakt, parser a validační report
- Do `QuizApp.Shared/Contracts/CsvQuizContract.cs` byl přidán explicitní CSV kontrakt se striktní hlavičkou `question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec`.
- V `QuizApp.Server/Application/QuizImport/QuizCsvParser.cs` vznikl parser/validátor s výstupem `CsvQuizImportParseResult` a strukturovanými chybami `CsvValidationIssueDto` (`row`, `column`, `message`).
- Implementováno ignorování prázdných řádků a validace povinných textových polí, `correct_option` (`A-D`) a `time_limit_sec` (`10-300`).
- V `QuizApp.Tests/UnitTest1.cs` nahrazen placeholder test sadou `QuizCsvParserTests` pokrývající validní CSV, chybnou hlavičku, chybné hodnoty i ignorování prázdných řádků.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (4 passed).
- Omezení: ruční smoke-check importu přes UI/API zatím neproběhl, protože endpointy/import služba budou až v navazujících krocích (`S08`, `S09`, `S10`).

## S08 — Služba pro založení kvízu a import otázek
- V `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` byla přidána aplikační služba `IQuizManagementService` + `QuizManagementService` s operacemi `CreateQuizAsync` a `ImportQuizCsvAsync`.
- `CreateQuizAsync` generuje `QuizOrganizerToken` s 256bit entropií, ukládá jen hash tokenu (`SHA-256`), hashuje `DeletePassword` (`PBKDF2-SHA256`) a zapisuje audit `QUIZ_CREATED`.
- `ImportQuizCsvAsync` ověřuje organizer token constant-time porovnáním hashů, povolí import jen pro prázdný kvíz, mapuje parse výstup na `Question`/`QuestionOption` a zapisuje audit `QUIZ_IMPORTED`.
- V `QuizApp.Server/Program.cs` doplněna DI registrace `IQuizCsvParser` a `IQuizManagementService`.
- V `QuizApp.Tests/QuizManagementServiceTests.cs` přidány testy create/import/token auth; v `QuizApp.Tests.csproj` přidán `Microsoft.EntityFrameworkCore.InMemory`.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (21 passed).

## S09 — REST endpointy pro správu kvízů
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` byly přidány endpointy `POST /api/quizzes`, `POST /api/quizzes/{quizId}/import-csv`, `GET /api/quizzes/{quizId}` a `DELETE /api/quizzes/{quizId}`.
- V `QuizApp.Server/Program.cs` je nyní namapováno `app.MapQuizManagementEndpoints()`.
- `QuizManagementService` byl rozšířen o operace `GetQuizDetailAsync` a `DeleteQuizAsync` včetně auditu `QUIZ_DELETED`, kontroly aktivních session (`WAITING`/`RUNNING`) a verifikace Administrátorkého hesla kvízu.
- Organizátorská autorizace byla sjednocena tak, že `import`, `detail` a `delete` přijímají `X-Organizer-Token` nebo `X-Quiz-Password`; smazání stále vyžaduje správné Administrátorké heslo kvízu.
- V `QuizApp.Shared/Contracts/QuizContracts.cs` vznikly DTO kontrakty pro detail kvízu (`QuizDetailResponse`, `QuizDetailQuestionDto`, `QuizDetailQuestionOptionDto`).
- V `QuizApp.Tests/QuizManagementServiceTests.cs` byly přidány testy pro autentizaci přes heslo, detail kvízu a mazání (včetně blokace při aktivní session).
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (26 passed).

## S10 — Organizátorské UI pro kvízy
- V `QuizApp.Client/Pages/OrganizerDashboard.razor` byl nahrazen placeholder za funkční organizátorský dashboard s formulářem pro vytvoření kvízu (`POST /api/quizzes`), jednorázovým zobrazením tokenu a lokálním seznamem uložených kvízů.
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` vzniklo minimum UI pro načtení detailu (`GET /api/quizzes/{quizId}`), upload/import CSV (`POST /api/quizzes/{quizId}/import-csv`) a zobrazení validačního reportu.
- Na stejné detail stránce bylo doplněno smazání kvízu (`DELETE /api/quizzes/{quizId}`) se zadáním Administrátorkého hesla kvízu.
- Přidána klientská služba `QuizApp.Client/Organizer/OrganizerQuizLocalStore.cs` pro ukládání dvojic `quizId + QuizOrganizerToken` do `localStorage`.
- `QuizApp.Client/Program.cs` registruje `OrganizerQuizLocalStore` do DI a `_Imports.razor` byl rozšířen o sdílené kontrakty/enumy.
- Hotfix: klientský `HttpClient` nyní čte `ApiBaseUrl` z `QuizApp.Client/wwwroot/appsettings.json`, aby WASM UI volalo server API i při běhu na jiném portu/originu.
- Hotfix: v `QuizApp.Server/Program.cs` přidána CORS policy `ClientOrigins` pro localhost originy, aby UI volání API nebyla blokována browserem.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (26 passed).
- Omezení: ruční smoke-check S10 UI flow v běžícím klientovi/serveru zatím neproběhl v tomto prostředí.

## S11 — Session create backend a join code
- V `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` byla doplněna operace `CreateSessionAsync`, která ověřuje organizátorskou autentizaci (`X-Organizer-Token` nebo `X-Quiz-Password`), kontroluje minimálně 1 otázku v kvízu a vytváří novou session ve stavu `WAITING`.
- Join code se generuje kryptograficky bezpečně (8 znaků z omezené alfanumerické abecedy bez nejednoznačných znaků) a před uložením se ověřuje jeho unikátnost.
- Při vytvoření session se zapisuje audit akce `SESSION_CREATED`.
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` byl přidán endpoint `POST /api/quizzes/{quizId}/sessions`.
- V `QuizApp.Tests/QuizManagementServiceTests.cs` přibyly testy pro pravidla S11: konflikt pro kvíz bez otázek, vytvoření session přes heslo bez tokenu a opakované create session nad stejným kvízem.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (29 passed).
- Omezení: ruční smoke-check endpointu S11 v běžícím serveru/UI zatím neproběhl v tomto prostředí.

## S12 — Team join backend a reconnect identita
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` byla přidána služba `ISessionParticipationService` s operacemi `JoinSessionAsync` a `GetSessionStateAsync`.
- `JoinSessionAsync` implementuje `POST /api/sessions/join`: validuje join code, stav `WAITING`, unikátní název týmu v session (case-insensitive), limit 20 týmů a vrací `teamId + TeamReconnectToken` pouze jednorázově při joinu.
- Reconnect token je generován kryptograficky bezpečně (256 bitů), na serveru se ukládá pouze jeho hash (`SHA-256`).
- `GetSessionStateAsync` implementuje `GET /api/sessions/{sessionId}/state?teamId={teamId}` s povinnou hlavičkou `X-Team-Reconnect-Token`, constant-time porovnáním hashů, aktualizací `LastSeenAtUtc` a vracením snapshotu session/teams/current question.
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` vzniklo mapování týmových endpointů a v `QuizApp.Server/Program.cs` byla doplněna DI registrace + mapování těchto endpointů.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy pro pravidla S12: validní join, duplicitní název týmu, neplatný join code a valid/invalid reconnect token pro state snapshot.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (34 passed).
- Omezení: ruční smoke-check nových S12 endpointů v běžícím serveru/klientovi zatím neproběhl v tomto prostředí.

## S13 — Organizátorský waiting room a session create UI
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` bylo doplněno vytvoření session z UI (`POST /api/quizzes/{quizId}/sessions`), zobrazení `SessionId + JoinCode` a přímý přechod na čekárnu.
- Placeholder `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` byl nahrazen funkční čekárnou organizátora s načtením snapshotu přes `GET /api/sessions/{sessionId}`, včetně zobrazení join code, stavu session a seznamu připojených týmů.
- V `QuizApp.Shared/Contracts/SessionContracts.cs` přibyl kontrakt `OrganizerSessionSnapshotResponse` pro waiting room data.
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` přibyl endpoint `GET /api/sessions/{sessionId}`.
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` přibyla operace `GetOrganizerSessionStateAsync` s autentizací přes `X-Organizer-Token` nebo `X-Quiz-Password` a vracením snapshotu session.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` byly přidány testy pro organizátorský snapshot (úspěch přes heslo, chyba bez autentizace).
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (36 passed).
- Omezení: ruční smoke-check S13 UI/API flow v běžícím klientovi/serveru zatím neproběhl v tomto prostředí.

## S14 — Start/cancel session backend
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` byly doplněny operace `StartSessionAsync` a `CancelSessionAsync` s validací přechodů stavů session, podmínkou alespoň 1 připojeného týmu pro start, autorizací přes `X-Organizer-Token` nebo `X-Quiz-Password`, ošetřením optimistic concurrency a zápisem audit akcí `SESSION_STARTED` / `SESSION_CANCELLED`.
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` byly přidány endpointy `POST /api/sessions/{sessionId}/start` a `POST /api/sessions/{sessionId}/cancel`.
- V `QuizApp.Shared/Contracts/SessionContracts.cs` byl přidán kontrakt `CancelSessionRequest` pro explicitní potvrzení zrušení session.
- V `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` byla přidána tlačítka pro spuštění a zrušení session, napojení na nové endpointy a potvrzovací dialog před zrušením.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy pokrývající S14 pravidla (start bez týmu, start s týmem, cancel bez potvrzení, cancel běžící session, zákaz mutace terminálního stavu).
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (41 passed).
- Omezení: ruční smoke-check S14 UI/API flow v běžícím klientovi/serveru zatím neproběhl v tomto prostředí.

## S15 — Otázkový engine a timeout progression
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` byl rozšířen `StartSessionAsync` tak, aby při přechodu do `RUNNING` okamžitě nastavil první otázku (`CurrentQuestionIndex`, `CurrentQuestionStartedAtUtc`, `QuestionDeadlineUtc`).
- Do stejné služby přibyla operace `ProgressDueSessionsAsync`, která serverově posouvá otázky po timeoutu a po poslední otázce přepíná session do `FINISHED`.
- V `QuizApp.Server/Application/Sessions/SessionProgressionBackgroundService.cs` vznikla hostovaná background služba, která periodicky volá timeout progression nad běžícími session.
- V `QuizApp.Server/Program.cs` byla doplněna registrace `SessionProgressionBackgroundService`.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy pro S15 (nastavení první otázky po startu, posun na další otázku po timeoutu, přechod do `FINISHED` po timeoutu poslední otázky).
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (43 passed).
- Omezení: ruční smoke-check časového průběhu S15 v běžícím serveru/klientovi zatím neproběhl v tomto prostředí.

## S16 — SignalR session groups a eventy
- V `QuizApp.Server/Application/Sessions/SessionHub.cs` vznikl SignalR hub se session-specific groups a metodami `SubscribeToSessionAsync` / `UnsubscribeFromSessionAsync`.
- V `QuizApp.Server/Application/Sessions/SessionRealtimePublisher.cs` byla přidána vrstva pro publikaci realtime eventů dle `RealtimeEventName` do konkrétní session group.
- `SessionParticipationService` nyní publikuje eventy `team.joined`, `session.started`, `question.changed`, `session.cancelled`, `session.finished`, `results.ready` po odpovídajících stavových změnách (join/start/cancel/progression).
- V `QuizApp.Server/Program.cs` je nově namapován hub endpoint `app.MapHub<SessionHub>("/hubs/sessions")` a registrován `ISessionRealtimePublisher`.
- `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` bylo rozšířeno o SignalR připojení s automatic reconnect, session resubscribe a refresh snapshotu přes REST při realtime eventech.
- V `QuizApp.Client/QuizApp.Client.csproj` byl přidán balíček `Microsoft.AspNetCore.SignalR.Client`.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy ověřující publikaci realtime eventů (`team.joined`, `session.finished`, `results.ready`).
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (45 passed).
- Omezení: ruční smoke-check realtime synchronizace ve dvou otevřených klientech zatím neproběhl v tomto prostředí.

## S17 — Team UI: join, waiting room, question screen
- Placeholder `QuizApp.Client/Pages/TeamJoin.razor` byl nahrazen funkčním join formulářem s voláním `POST /api/sessions/join`, zpracováním chyb a uložením týmové identity do `localStorage`.
- Placeholder `QuizApp.Client/Pages/TeamWaitingRoom.razor` byl nahrazen funkční čekárnou týmu s načtením snapshotu přes `GET /api/sessions/{sessionId}/state?teamId={teamId}`, hlavičkou `X-Team-Reconnect-Token`, realtime subscribe do session group a přechodem na otázku po startu session.
- Placeholder `QuizApp.Client/Pages/TeamQuestion.razor` byl nahrazen otázkovou obrazovkou se snapshot loadingem, vykreslením odpovědí `A/B/C/D`, lokálním jednorázovým uzamčením odpovědi po odeslání a realtime refresh při `question.changed`/ukončovacích eventech.
- V `QuizApp.Client/Team/TeamSessionLocalStore.cs` vznikla klientská služba pro lokální uložení týmové identity (`sessionId + teamId + teamName + TeamReconnectToken`) a lokálně uzamčených odpovědí per otázka (`sessionId + questionId`).
- V `QuizApp.Client/Program.cs` byla doplněna DI registrace `TeamSessionLocalStore`; `_Imports.razor` nyní obsahuje `@using QuizApp.Client.Team`.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (45 passed).
- Omezení: backend submit odpovědi (`POST /api/sessions/{sessionId}/answers`) ještě není implementován (krok `S18`), proto je v `S17` „odeslání“ řešeno pouze lokálním uzamčením UI.

## S18 — Answer submit backend
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` byla přidána operace `SubmitAnswerAsync` s validací vstupu, autentizací přes `X-Team-Reconnect-Token`, kontrolou stavu `RUNNING`, validací aktivní otázky a deadline a uložením odpovědi s výpočtem `IsCorrect` + `ResponseTimeMs`.
- First-write-wins je vynuceno kombinací aplikační kontroly a databázové unikátnosti (`SessionId + TeamId + QuestionId`) s mapováním kolize na business chybu `AlreadyAnswered`.
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` byl přidán endpoint `POST /api/sessions/{sessionId}/answers`.
- V `QuizApp.Shared/Contracts/SessionContracts.cs` byly přidány kontrakty `SubmitAnswerRequest` a `SubmitAnswerResponse`.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy pro S18: valid submit, duplicate submit, late submit a invalid reconnect token.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (49 passed).
- Omezení: ruční smoke-check submit flow v běžícím klientovi/serveru zatím neproběhl v tomto prostředí.

## S19 — Výsledky, ranking a correct answers
- V `QuizApp.Shared/Contracts/SessionContracts.cs` přibyly kontrakty `SessionResultsResponse`, `SessionResultDto`, `CorrectAnswersResponse`, `CorrectAnswerDto` pro přenos výsledků a správných odpovědí.
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` přibyly operace `GetSessionResultsAsync` (dual-auth: tým nebo organizátor), `GetCorrectAnswersAsync` (pouze organizátor) a privátní `ComputeSessionResultsAsync` (ranking: skóre DESC, celkový čas správných odpovědí ASC, sdílený rank při shodě).
- `ProgressDueSessionsAsync` nyní při finalizaci session automaticky počítá a ukládá `SessionResult` entity; query rozšířeno o `.Include(x => x.Teams)`.
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` přibyly endpointy `GET /api/sessions/{sessionId}/results` a `GET /api/sessions/{sessionId}/correct-answers`.
- `QuizApp.Client/Pages/TeamQuestion.razor` opraven tak, aby volal backend submit (`POST /api/sessions/{sessionId}/answers` s `X-Team-Reconnect-Token`) místo pouze lokálního uzamčení ze S17; rozlišuje FINISHED (přechod na výsledky) a CANCELLED (přechod na hlavní stránku).
- Placeholder `QuizApp.Client/Pages/SessionResults.razor` nahrazen funkční stránkou výsledků pro tým s ranked tabulkou a zvýrazněním vlastního týmu.
- `QuizApp.Client/Pages/OrganizerSessionResults.razor` vytvořena stránka výsledků pro organizátora s tabulkou rankingu a přehledem správných odpovědí (správná varianta zvýrazněna zeleně).
- `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` doplněn o odkaz „Zobrazit výsledky a správné odpovědi" při stavu FINISHED.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibylo 9 nových testů S19 (výpočet a uložení výsledků, ranked výsledky pro tým/organizátora, chybějící auth, pre-FINISHED odmítnutí, správné odpovědi, tie-break).
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (58/58 passed).
- Omezení: ruční smoke-check results/ranking flow v běžícím klientovi/serveru zatím neproběhl v tomto prostředí.

## Bugfix — Realtime rendering a countdown timer
- V `QuizApp.Client/Pages/TeamQuestion.razor` přidáno explicitní volání `StateHasChanged()` na konci `ReloadSnapshotFromRealtimeAsync`, čímž se opravilo nezobrazení nové otázky po přijetí SignalR eventu `question.changed`.
- Tentýž rendering fix aplikován i v `QuizApp.Client/Pages/TeamWaitingRoom.razor` a `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` (prevence stejné chyby).
- V `QuizApp.Client/Pages/TeamQuestion.razor` přidán živý countdown timer (zbývající sekundy do deadline), který se aktualizuje každou sekundu přes `System.Threading.Timer` + `InvokeAsync(StateHasChanged)`.
- Countdown se automaticky restartuje při přechodu na novou otázku (z `ReloadSnapshotFromRealtimeAsync` i `OnParametersSetAsync`).
- Timer se korektně uvolňuje v `DisposeAsync`.
- Nahrazeno zobrazení surového UTC deadline textem „⏱ Zbývá: Xs" / „⏱ Čas vypršel".
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (58/58 passed).
- Omezení: ruční smoke-check vyžaduje běžící server + klienta s aktivní RUNNING session.

## S20 — Hardening a bezpečnostní minimum
- V `QuizApp.Server/Program.cs` byl doplněn built-in ASP.NET Core rate limiting (`AddRateLimiter` + `UseRateLimiter`) s politikami: `JoinPerIp` (10 req/min), `SubmitPerTeam` (20 req/min) a `OrganizerMutations` (10 req/min).
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` a `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` byly mutační endpointy navázány na odpovídající limiter policy přes `RequireRateLimiting(...)`.
- Pro HTTPS/TLS-ready běh mimo localhost byl v server pipeline doplněn `UseForwardedHeaders()` a `UseHsts()` (mimo development).
- V `QuizApp.Server/Application/Common/TextInputSanitizer.cs` vznikla centralizovaná sanitizace single-line textových vstupů; `QuizManagementService` a `SessionParticipationService` ji používají pro názvy kvízu/týmu včetně délkových validací (`Quiz.Name <= 200`, `Team.Name <= 120`).
- V `QuizApp.Client/Realtime/ReconnectWithinSixtySecondsPolicy.cs` byla přidána reconnect policy s max. reconnect oknem 60 sekund a byla použita ve stránkách `OrganizerWaitingRoom`, `TeamWaitingRoom`, `TeamQuestion`.
- V testech (`QuizManagementServiceTests`, `SessionParticipationServiceTests`) přibyly scénáře ověřující sanitizaci a délkové validace textových vstupů.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (62/62 passed).
- Omezení: ruční smoke-check rate limitingu a reconnect chování vyžaduje běžící server/klienta v interaktivním prostředí.

## S21 — Testy a release readiness
- V `QuizApp.Tests/ApiIntegrationTests.cs` přibyla API integrační test sada nad reálným HTTP hostem (`WebApplicationFactory`):
  - end-to-end organizátorský flow (`POST /api/quizzes` -> `POST /import-csv` přes heslo -> `POST /sessions` -> `POST /api/sessions/join` -> `GET /api/sessions/{sessionId}`),
  - ověření auth invariantu pro týmový snapshot (`GET /api/sessions/{sessionId}/state`) pro missing/invalid/valid `X-Team-Reconnect-Token`.
- V `QuizApp.Server/Program.cs` je pro testovatelnost doplněno přeskočení startup migrace v prostředí `Testing` a export `public partial class Program` pro `WebApplicationFactory<Program>`.
- V `QuizApp.Tests/QuizApp.Tests.csproj` byl přidán balíček `Microsoft.AspNetCore.Mvc.Testing`.
- Vznikl finální checklist připravenosti release v `.github/context/11-release-checklist.md`.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (64/64 passed).
- Omezení: ruční smoke-check v reálném běhu klient/server a verifikace `database update` proti dostupnému PostgreSQL zůstávají mimo toto prostředí.

## Post-S21 — Ověření databázového dluhu
- Byl ručně spuštěn a úspěšně dokončen `dotnet dotnet-ef database update` pro `QuizApp.Server` v prostředí `Development`.
- V `08-implementation-state.md` byl odpovídající dluh odstraněn ze sekce `Rizika / dluh` a přesunut do sekce ověření.
- V `11-release-checklist.md` je položka ověření DB migrace označena jako splněná.

## Bugfix — Team answer lock pouze po serverovém potvrzení a per tým
- V `QuizApp.Client/Pages/TeamQuestion.razor` byl upraven submit flow: lokální lock odpovědi se ukládá až po úspěšném `POST /api/sessions/{sessionId}/answers` (a také při `AlreadyAnswered`), nikoliv před odesláním requestu.
- V `QuizApp.Client/Team/TeamSessionLocalStore.cs` bylo u lokálně uložených odpovědí doplněno `TeamId`; lookup i zápis odpovědi je nově vázán na `sessionId + teamId + questionId`.
- Tím se odstranila situace, kdy neúspěšný submit lokálně uzamkl otázku a tým následně končil s nulovým skóre bez reálného uložení odpovědi na server.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (64/64 passed).

## Bugfix — Stabilní team identity v rámci jedné session i ve stejném browseru
- `QuizApp.Client/Team/TeamSessionLocalStore.cs` nově neomezuje identity na jednu položku per session; identity jsou ukládány per `TeamId` a vyhledání identity podporuje parametr `teamId`.
- `QuizApp.Client/Pages/TeamJoin.razor` po úspěšném joinu přesměrovává na čekárnu s query `?teamId={teamId}`.
- `QuizApp.Client/Pages/TeamWaitingRoom.razor`, `QuizApp.Client/Pages/TeamQuestion.razor` a `QuizApp.Client/Pages/SessionResults.razor` přijímají `teamId` z query a načítají správnou identitu přes `FindIdentityAsync(sessionId, teamId)`.
- Navigace `čekárna -> otázka -> výsledky` přenáší `teamId`, aby se odpovědi i výsledky mapovaly na správný tým.
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (64/64 passed).

## UI úprava — Organizátorský dashboard: seznam nahoře, nový kvíz dole a bez ID/tokenu v gridu
- V `QuizApp.Client/Pages/OrganizerDashboard.razor` je nově pořadí sekcí: nejdřív seznam uložených kvízů, až pod ním formulář „Nový kvíz".
- Tabulka uložených kvízů zobrazuje pouze sloupce `Název kvízu` a `Datum založení`; interní `QuizId` i `Organizer token` už se v gridu nezobrazují.
- Název kvízu v tabulce zůstává proklik na detail (`/organizator/kviz/{quizId}`), takže funkčnost detailu je zachovaná bez extra sloupce.
- `QuizApp.Client/Organizer/OrganizerQuizLocalStore.cs` byl rozšířen o metadata `QuizName` a `CreatedAtUtc`; dashboard je pro starší položky bez metadat doplní přes `GET /api/quizzes/{quizId}` s `X-Organizer-Token`.

## Post-S21 UX tweak — CSV předpis ke stažení v import formuláři
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byl v sekci `Import otázek CSV` přidán odkaz `Stáhnout CSV předpis (šablonu)`.
- Vznikl soubor `QuizApp.Client/wwwroot/templates/quiz-question-import-template.csv` s rozšířenou hlavičkou (`question_type`, `correct_numeric_value`) a ukázkovými řádky pro typy `choice` i `numeric`.
- Tím je pro organizátora dostupný okamžitý předpis správného CSV formátu přímo v import formuláři.
- Ověřeno buildem solution (`run_build`).

## UI úprava — Organizátorský dashboard: návrat tlačítka Detail
- V `QuizApp.Client/Pages/OrganizerDashboard.razor` bylo do buňky `Název kvízu` vráceno tlačítko `Detail` (`btn btn-sm btn-outline-primary`) vedle názvu.
- Grid stále obsahuje jen dva datové sloupce (`Název kvízu`, `Datum založení`), bez `QuizId` a tokenu.
- Ověřeno buildem solution (`run_build`).

## UI úprava — Organizátorský dashboard: `Detail` úplně napravo
- V `QuizApp.Client/Pages/OrganizerDashboard.razor` bylo tlačítko `Detail` přesunuto do pravé části druhého sloupce (`Datum založení`) a zarovnáno na pravý okraj řádku.
- Struktura tabulky zůstává dvousloupcová (`Název kvízu`, `Datum založení`) bez zobrazení `QuizId` a tokenu.
- Ověřeno buildem solution (`run_build`).

## UI/API úprava — Join kód při startu kvízu + automatický snapshot čekárny
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byl do sekce `Spustit kvíz` přidán textbox pro zadání join kódu, který se odesílá v body requestu při vytvoření session.
- V `QuizApp.Shared/Contracts/SessionContracts.cs` byl kontrakt `CreateSessionRequest` upraven na `JoinCode`.
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` a `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` nyní endpoint/session create přijímá join kód od organizátora, vyžaduje minimálně 4 znaky a kontroluje unikátnost.
- V `QuizApp.Client/Pages/OrganizerWaitingRoom.razor` bylo doplněno automatické načtení snapshotu při příchodu na stránku se `SessionId`, takže odpadá ruční kliknutí na `Načíst snapshot` po startu kvízu.
- Aktualizovány testy `QuizApp.Tests/QuizManagementServiceTests.cs`, `QuizApp.Tests/SessionParticipationServiceTests.cs` a `QuizApp.Tests/ApiIntegrationTests.cs` na nový create-session kontrakt.
- Ověřeno buildem solution (`run_build`) a test runem projektu `QuizApp.Tests` (`run_tests`, 64/64 passed).

## Bugfix — Join kód při startu session bez formátových omezení
- V `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` byla validace join kódu upravena tak, aby jediným pravidlem byla minimální délka 4 znaky.
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byla upravena nápověda i klientská validace join kódu na stejné pravidlo (4+ znaků).
- Byla odstraněna dřívější omezení na pevnou délku 8 znaků a konkrétní znakovou sadu.
- Ověřeno buildem solution (`run_build`) a test runem projektu `QuizApp.Tests` (`run_tests`, 64/64 passed).

## Bugfix — Chybějící EF migrace pro `NumericClosest` a pád background služby
- V `QuizApp.Server/Persistence/Migrations` byla vytvořena migrace `20260331190153_AddNumericClosestQuestionFields` (+ designer + update `QuizAppDbContextModelSnapshot`).
- Migrace přidává sloupce `Questions.CorrectNumericValue`, `Questions.QuestionType`, `TeamAnswers.NumericValue` a mění `Questions.CorrectOption` + `TeamAnswers.SelectedOption` na nullable pro podporu numerických odpovědí.
- Byl spuštěn `dotnet ef database update --project QuizApp.Server --startup-project QuizApp.Server` a migrace byla úspěšně aplikována na Development PostgreSQL.
- Ověřeno buildem solution (`run_build`); tím je odstraněna runtime chyba `42703: column q0.CorrectNumericValue does not exist`, která shazovala `SessionProgressionBackgroundService`.

## Post-S21 feature — Ruční vkládání otázek v detailu kvízu
- V `QuizApp.Shared/Contracts/QuizContracts.cs` přidány kontrakty `AddQuizQuestionRequest` a `AddQuizQuestionResponse`.
- V `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` přidána operace `AddQuestionAsync` pro ruční vložení otázky (validace choice/numeric, sanitizace textů, pořadí `OrderIndex`, audit `QUESTION_ADDED`).
- Ruční úpravy otázek jsou záměrně blokované při aktivní session (`WAITING` nebo `RUNNING`), aby se během hry neměnila sada otázek.
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` přidán endpoint `POST /api/quizzes/{quizId}/questions` s organizátorskou autentizací (`X-Organizer-Token` nebo `X-Quiz-Password`) a limiter policy `OrganizerMutations`.
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` v sekci `Otázky kvízu` přidán formulář pro ruční vložení otázky (typ, text, čas, A-D/správná varianta nebo správná číselná hodnota) s odesláním na API a automatickým refresh seznamu.
- V `QuizApp.Tests/QuizManagementServiceTests.cs` přidány testy pro multiple-choice insert, numeric insert a blokaci při aktivní session; v `QuizApp.Tests/ApiIntegrationTests.cs` přidán HTTP test endpointu.
- Ověřeno buildem solution (`run_build`) a test runem projektu `QuizApp.Tests` (`run_tests`, 77/77 passed).

## Post-S21 UI bugfix — Zobrazení desetinných čísel bez zbytečných nul
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` je správná numerická hodnota otázky formátována bez koncových nul.
- V `QuizApp.Client/Pages/OrganizerSessionResults.razor` a `QuizApp.Client/Pages/SessionResults.razor` jsou numerické hodnoty (`CorrectNumericValue`, týmový numerický tip) formátovány bez nulového paddingu.
- Ve výsledcích byl upraven i formát času v sekundách: při detailním režimu se používá `0.###` namísto `F3`, takže se nezobrazují hodnoty typu `12.000`.
- V `QuizApp.Client/Pages/TeamQuestion.razor` je potvrzení odeslaného numerického tipu zobrazeno bez zbytečných nul.
- Ověřeno buildem solution (`run_build`) a test runem projektu `QuizApp.Tests` (`run_tests`, 77/77 passed).

## Post-S21 backend bugfix — `CorrectCount` u numerických otázek pouze za přesnou shodu
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` byl upraven výpočet `ComputeSessionResultsAsync` pro `QuestionType.NumericClosest`:
  - `Score` zůstává za nejbližší tip(y),
  - `CorrectCount` se zvyšuje pouze při přesné shodě `NumericValue == CorrectNumericValue`.
- Tím se v gridu výsledků sloupec `Správně` počítá jen ze skutečně správných odpovědí, zatímco bod za nejbližší nepřesný tip zůstává zachován ve sloupci `Body`.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přidán test `NumericClosest_NearestButNotExact_GivesScoreButNotCorrectCount`.
- Ověřeno buildem solution (`run_build`) a test runem projektu `QuizApp.Tests` (`run_tests`, 78/78 passed).

## Post-S21 UI text tweak — `Správná odpověď` u numerických odpovědí
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor`, `QuizApp.Client/Pages/OrganizerSessionResults.razor` a `QuizApp.Client/Pages/SessionResults.razor` byl text `Správná hodnota:` změněn na `Správná odpověď:`.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — `Otázky kvízu`: okamžité ruční vložení u prázdného kvízu
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byla upravena sekce `Otázky kvízu` tak, aby se při `QuestionCount == 0` zobrazil formulář `Ruční vložení otázky` ihned, bez mezikroku `Zobrazit otázky`.
- Pro kvízy, které už obsahují otázky, zůstává původní chování beze změny (nejdříve zadání hesla a klik na `Zobrazit otázky`, potom zobrazení seznamu i formuláře).
- Po přidání první otázky bez načteného seznamu se stránka přepne zpět do původního režimu (kvíz už má otázky), takže další práce pokračuje standardním flow.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Pokračování ručního vkládání po první otázce bez hesla (jen v aktuálním otevření)
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` přidán dočasný UI stav `allowManualAddWithoutPassword`, který se aktivuje po úspěšném ručním přidání první otázky v prázdném kvízu.
- V tomto stavu zůstává formulář `Ruční vložení otázky` dál viditelný a nezobrazí se mezikrok se zadáním hesla, takže organizátor může hned pokračovat další otázkou.
- Po novém otevření detailu se tento stav resetuje a opět platí standardní flow se zadáním hesla pro zobrazení otázek.
- Ověřeno buildem solution (`run_build`).

## Post-S21 feature — Ruční pořadí a editace existujících otázek
- V `QuizApp.Shared/Contracts/QuizContracts.cs` byl `AddQuizQuestionRequest` rozšířen o `Order` a přidán kontrakt `UpdateQuizQuestionRequest`.
- V `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` byla přidána operace `UpdateQuestionAsync`; u `POST` i `PUT` je validována kolize pořadí (duplicitní pořadí v rámci kvízu vrací validační chybu).
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` byl doplněn endpoint `PUT /api/quizzes/{quizId}/questions/{questionId}`.
- V `QuizApp.Server/Domain/Entities/Question.cs` a `QuizApp.Server/Domain/Entities/QuestionOption.cs` přibyly doménové metody pro update otázky a textů odpovědí.
- `QuizApp.Client/Pages/OrganizerQuizDetail.razor` nyní umožňuje zadat `Pořadí otázky` při ručním vložení a také upravit existující otázku přes tlačítko `Upravit`.
- V `QuizApp.Tests/QuizManagementServiceTests.cs` přidány testy `AddQuestionAsync_DuplicateOrder_ReturnsValidationFailed` a `UpdateQuestionAsync_ValidRequest_UpdatesQuestionAndOrder`.
- V `QuizApp.Tests/ApiIntegrationTests.cs` přidán integrační test `UpdateQuestionEndpoint_ValidatesDuplicateOrder`.
- Ověřeno buildem solution (`run_build`) a test runem projektu `QuizApp.Tests` (`run_tests`, 81/81 passed).

## Post-S21 UI tweak — `Pořadí otázky` je první ve formuláři ručního vložení
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` bylo pole `Pořadí otázky` přesunuto nad `Typ otázky`, aby bylo při ručním vkládání/editaci první.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Sbalitelný formulář `Ruční vložení otázky`
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` je řádek s nadpisem `Ruční vložení otázky` rozšířen o malé tlačítko vpravo, které formulář rozbalí/sbalí.
- Celý obsah formuláře (pole, volby typu otázky i akční tlačítka) se vykresluje jen při rozbaleném stavu.
- Při kliknutí na `Upravit` u existující otázky se formulář automaticky rozbalí, aby byla úprava okamžitě dostupná.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Směr šipky toggle tlačítka formuláře
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` je šipka v toggle tlačítku upravena na `▼` při sbaleném formuláři a `▲` při rozbaleném formuláři.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Jemné podbarvení jednotlivých položek v `Otázky kvízu`
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byla jednotlivým otázkám v seznamu přidána třída `quiz-question-item`.
- V `QuizApp.Client/wwwroot/css/app.css` byl doplněn styl s jemným podbarvením (`background-color`) a lehce upravenou barvou border; položky `list-group-item` uvnitř otázky mají transparentní pozadí.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Viditelnější podbarvení položek v `Otázky kvízu`
- V `QuizApp.Client/wwwroot/css/app.css` byl styl `quiz-question-item` upraven na výraznější kontrastní podklad (`#eef4ff`), světle modrý border a jemnou vnitřní linku (`inset`), aby rozdíl oproti pozadí byl jasně patrný.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Přesun stylu otázek do komponentního CSS
- Styly `quiz-question-item` byly přesunuty z `QuizApp.Client/wwwroot/css/app.css` do `QuizApp.Client/Pages/OrganizerQuizDetail.razor.css`.
- Cílem je zajistit konzistentní aplikaci podbarvení přímo v komponentě `OrganizerQuizDetail` přes CSS isolation.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Inline fallback pro podbarvení otázek
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` je podbarvení question card (`background`, `border`, `inset`) nastaveno přímo inline stylem na `<article>`.
- U řádků odpovědí (`li.list-group-item`) je navíc inline nastaveno `background-color: transparent`, aby zůstal kontrast podkladové card i u multiple-choice variant.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Badge `Správná` se zeleným textem
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byl badge u správné odpovědi změněn z `text-bg-success` na `bg-light text-success border border-success`.
- Text badge `Správná` je nyní zelený a vizuálně konzistentní se zeleným zvýrazněním správné odpovědi.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Nový layout question card v `Otázky kvízu`
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byly informace v kartě otázky uspořádány do požadovaného pořadí: `Otázka číslo:`, text otázky, `Typ otázky:`, `Čas na odpověď:`.
- Tlačítko `Upravit` je nyní v pravém horním rohu každé karty otázky.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Text o heslu se skrývá u prázdného kvízu
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` je text „Otázky i správné odpovědi se zobrazí až po zadání Administrátorského hesla kvízu.“ zobrazen jen pokud má kvíz alespoň jednu otázku.
- U prázdného kvízu se text nyní nezobrazuje, aby neblokoval/neznejasňoval flow ručního vložení první otázky.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — `Import otázek CSV` přesunut pod `Otázky kvízu`
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byla sekce `Import otázek CSV` přesunuta pod sekci `Otázky kvízu` (nad sekci `Smazání kvízu`).
- Ověřeno buildem solution (`run_build`).

## Post-S21 backend validation — Kontrola kompletního pořadí před spuštěním kvízu
- V `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` byla do `CreateSessionAsync` doplněna validace, že pořadí otázek je souvislé (`OrderIndex` bez mezer od 0).
- Pokud pořadí není kompletní, vrací se `ValidationFailed` s hláškou „Kvíz není možné spustit, protože neobsahuje kompletní pořadí otázek.“ a session se nevytvoří.
- V `QuizApp.Tests/QuizManagementServiceTests.cs` přidán test `CreateSessionAsync_IncompleteQuestionOrder_ReturnsValidationFailed`.
- Ověřeno buildem solution (`run_build`) a test runem projektu `QuizApp.Tests` (`run_tests`).

## Post-S21 UI validation — Frontend validace formuláře ručního vložení/úpravy otázky
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byla přidána klientská validace před odesláním (`Pořadí`, `Text otázky`, `Časový limit`, A-D odpovědi a správná odpověď u multiple-choice, správná číselná hodnota u numeric).
- Chybové zprávy jsou stručné a zobrazují se přímo pod konkrétními poli formuláře.
- Při validační chybě se request na API vůbec neposílá.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UI tweak — Automatické předvyplnění nejnižšího volného pořadí otázky
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byl přidán helper `GetNextAvailableManualOrder()`.
- Pole `Pořadí otázky` se po uložení otázky (a po načtení otázek) nastavuje na nejnižší volnou hodnotu místo prostého `count + 1`.
- Ověřeno buildem solution (`run_build`).

## Post-S21 UX/API tweak — CSV import používá delimiter `;`
- V `QuizApp.Server/Application/QuizImport/QuizCsvParser.cs` byl CSV parser přepnut na oddělovač `;` místo `,`.
- Parser nyní podporuje i volitelný první řádek `sep=;` (Excel directive), který při importu ignoruje.
- V `QuizApp.Tests/UnitTest1.cs` byly aktualizovány parser testy na semicolon formát a přidán test `Parse_WithExcelSeparatorDirective_IsAccepted`.
- Šablona `QuizApp.Client/wwwroot/templates/quiz-question-import-template.csv` byla upravena na semicolon hlavičku i data a obsahuje `sep=;`.
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` je v sekci importu doplněna informace, že CSV používá středník.
- Ověřeno buildem solution (`run_build`) a testy (`run_tests`: `Parse_WithExcelSeparatorDirective_IsAccepted`, `ImportQuizCsvAsync_ValidCsv_ImportsQuestionsAndCreatesAuditLog`, `ImportQuizCsvAsync_ValidPasswordWithoutToken_ImportsQuestions`, `OrganizerPassword_AllowsCreateImportCreateSessionAndSnapshotFlow`).

## Post-S21 UI/API tweak — Smazání otázky při úpravě otázky
- V `QuizApp.Server/Application/Quizzes/QuizManagementService.cs` přibyla operace `DeleteQuestionAsync` s autorizací (`X-Organizer-Token` nebo `X-Quiz-Password`), blokací mutací při aktivní session (`WAITING`/`RUNNING`), audit logem `QUESTION_DELETED` a reindexací `OrderIndex` u zbývajících otázek.
- V `QuizApp.Server/Application/Quizzes/QuizManagementEndpoints.cs` byl přidán endpoint `DELETE /api/quizzes/{quizId}/questions/{questionId}` (rate limit `OrganizerMutations`).
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` má formulář úpravy otázky nové tlačítko `Smazat otázku`, které maže právě editovanou otázku a po úspěchu obnoví seznam.
- V `QuizApp.Tests/QuizManagementServiceTests.cs` a `QuizApp.Tests/ApiIntegrationTests.cs` přibyly testy `DeleteQuestionAsync_ValidRequest_DeletesQuestionAndReindexesOrder` a `DeleteQuestionEndpoint_RemovesQuestionFromQuizDetail`.
- Ověřeno buildem solution (`run_build`) a cílenými testy (`run_tests` pro 2 nové testy).

## Post-S21 UX tweak — Okamžité zobrazení otázek po CSV importu
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byl upraven `ImportCsvAsync`: po úspěšném importu (s `ImportedQuestionsCount > 0`) se automaticky volá `LoadQuestionsAsync`, takže otázky jsou ihned viditelné v sekci `Otázky kvízu`.
- `LoadQuestionsAsync` nyní podporuje načtení otázek i přes uložený `X-Organizer-Token` (bez nutnosti vyplnit `X-Quiz-Password`), pokud je token k dispozici.
- Ověřeno buildem solution (`run_build`).

## Post-S21 bugfix — CSV diakritika z Excel/ANSI exportů
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byl upload CSV změněn z `StreamReader` na čtení raw bytes + explicitní dekódování.
- Dekódování nyní používá UTF-8 strict (`throwOnInvalidBytes=true`) a při selhání fallback na Windows-1250, takže se správně načtou české znaky i u ne-UTF8 souborů.
- V `QuizApp.Client/Program.cs` je registrován `CodePagesEncodingProvider`, aby byl fallback dekodér dostupný i ve WASM runtime.
- Ověřeno buildem solution (`run_build`) a test runem projektu `QuizApp.Tests` (`run_tests`, 85/85 passed).

## Post-S21 UI tweak — Po smazání otázky se formulář ručního vložení automaticky sbalí
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` byl upraven `DeleteEditedQuestionAsync`: po úspěšném smazání otázky se kromě resetu formuláře nastaví i `isManualQuestionFormExpanded = false`.
- Tlačítko `Smazat otázku` nyní po dokončení akce vrátí UI do sbaleného stavu sekce `Ruční vložení otázky`.

## R01 — Specifikace reconnect stavů a UX contract
- V `.github/context/09-decision-log.md` byl doplněn záznam `D-065` definující normativní reconnect stavový automat pro tým i organizátora: `Online`, `Reconnecting`, `Offline`, `Resynced`, `SessionEnded`.
- Byly sjednoceny UI hlášky a povolené akce pro každý stav (včetně retry/resync flow) a explicitně popsány role-specific pravidla pro tým i organizátora.
- Byl doplněn zákaz dead-end stavů: klient vždy nabízí recovery akci a po reconnectu přepisuje view autoritativním snapshotem serveru.
- Ověřeno buildem solution (`run_build`).
- Ověření testů: `run_tests` pro `QuizApp.Tests` aktuálně neprochází (`47/99 passed`, `52 failed`) — mimo scope kroku `R01`, zaznamenáno ve stavu implementace.

## R02 — Server-side přítomnost a heartbeat pro týmy i organizátora
- V `QuizApp.Server/Application/Sessions/SessionParticipationEndpoints.cs` jsou aktivní heartbeat endpointy `POST /api/sessions/{sessionId}/heartbeat/team` a `POST /api/sessions/{sessionId}/heartbeat/organizer` s limiter policy `HeartbeatPerParticipant`.
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` je server-side presence klasifikace `Connected` / `TemporarilyDisconnected` / `Inactive` podle `LastSeenAtUtc` bez zásahu do výpočtu skóre.
- Ve stejné službě bylo doplněno minimální auditování presence troubleshootingu včetně eventu `ORGANIZER_DISCONNECTED` při přechodu organizátora do odpojeného stavu.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` byl přidán test `GetOrganizerSessionStateAsync_StaleOrganizer_WritesSingleDisconnectedAudit`, který ověřuje zápis a neduplikování disconnect auditu.
- Ověřeno buildem solution (`run_build`) a cílenými testy (`run_tests`, 2/2 passed):
  - `HeartbeatOrganizerAsync_ValidAuth_WritesHeartbeatAudit`
  - `GetOrganizerSessionStateAsync_StaleOrganizer_WritesSingleDisconnectedAudit`

## R03 — Verze snapshotu a deterministická resynchronizace
- V `QuizApp.Shared/Contracts/SessionContracts.cs` jsou kontrakty `SessionStateSnapshotResponse` a `OrganizerSessionSnapshotResponse` rozšířené o `Version` a `ServerUtcNow`.
- V `QuizApp.Server/Application/Sessions/SessionParticipationService.cs` server vrací snapshot metadata (`Version`, `ServerUtcNow`) pro team i organizer snapshot; `Version` je monotónně odvozena z UTC ticks serverového času.
- V `QuizApp.Client/Pages/OrganizerWaitingRoom.razor`, `QuizApp.Client/Pages/TeamWaitingRoom.razor` a `QuizApp.Client/Pages/TeamQuestion.razor` je přidaný stale-response guard: snapshot se aplikuje jen pokud není starší než aktuální verze v UI.
- V `QuizApp.Tests/SessionParticipationServiceTests.cs` přibyly testy `GetSessionStateAsync_ValidToken_ReturnsSnapshotVersionMetadata` a `GetOrganizerSessionStateAsync_ValidToken_ReturnsSnapshotVersionMetadata`.
- Ověřeno buildem solution (`run_build`) a cílenými testy (`run_tests`, 4/4 passed):
  - `GetSessionStateAsync_ValidToken_ReturnsSnapshotVersionMetadata`
  - `GetOrganizerSessionStateAsync_ValidToken_ReturnsSnapshotVersionMetadata`
  - `HeartbeatOrganizerAsync_ValidAuth_WritesHeartbeatAudit`
  - `GetOrganizerSessionStateAsync_StaleOrganizer_WritesSingleDisconnectedAudit`

## R10 — Provozní observabilita reconnectu
- Vytvořen `QuizApp.Server/Application/Sessions/ReconnectMetrics.cs` — in-memory singleton s thread-safe čítači (`TeamReconnectCount`, `OrganizerReconnectCount`, `SnapshotServedCount`, `DuplicateSubmitRetryCount`, `FailedResyncCount`), sledováním resync doby a kruhovou frontou posledních 200 `ReconnectEvent`.
- Vytvořen `QuizApp.Server/Application/Sessions/DiagnosticsEndpoints.cs` — REST endpointy `GET /api/diagnostics/reconnect-metrics` a `POST /api/diagnostics/reconnect-metrics/reset`.
- V `SessionParticipationService` doplněn `IReconnectMetrics` a `ILogger`; instrumentace v 6 bodech: team reconnect, failed resync, snapshot served, resync duration, duplicate submit retry, organizer reconnect.
- V `SessionHub` doplněn `ILogger` s logy pro subscribe/unsubscribe/connect/disconnect.
- V `SessionRealtimePublisher` doplněn `ILogger` s debug logem před každým publish eventem.
- V `SessionProgressionBackgroundService` doplněn `ILogger`, try/catch s error logováním a info logy pro start/stop.
- V `Program.cs` registrován `IReconnectMetrics` jako singleton a namapovány diagnostické endpointy.
- V `SessionParticipationServiceTests.cs` aktualizován helper o `new ReconnectMetrics()` a `NullLogger`.
- Doporučené alert prahy dokumentovány v XML komentářích.
- Ověřeno: build OK, 124/124 testů prochází.
