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
- `QuizManagementService` byl rozšířen o operace `GetQuizDetailAsync` a `DeleteQuizAsync` včetně auditu `QUIZ_DELETED`, kontroly aktivních session (`WAITING`/`RUNNING`) a verifikace mazacího hesla.
- Organizátorská autorizace byla sjednocena tak, že `import`, `detail` a `delete` přijímají `X-Organizer-Token` nebo `X-Quiz-Password`; smazání stále vyžaduje správné mazací heslo.
- V `QuizApp.Shared/Contracts/QuizContracts.cs` vznikly DTO kontrakty pro detail kvízu (`QuizDetailResponse`, `QuizDetailQuestionDto`, `QuizDetailQuestionOptionDto`).
- V `QuizApp.Tests/QuizManagementServiceTests.cs` byly přidány testy pro autentizaci přes heslo, detail kvízu a mazání (včetně blokace při aktivní session).
- Ověřeno buildem solution a test runem projektu `QuizApp.Tests` (26 passed).

## S10 — Organizátorské UI pro kvízy
- V `QuizApp.Client/Pages/OrganizerDashboard.razor` byl nahrazen placeholder za funkční organizátorský dashboard s formulářem pro vytvoření kvízu (`POST /api/quizzes`), jednorázovým zobrazením tokenu a lokálním seznamem uložených kvízů.
- V `QuizApp.Client/Pages/OrganizerQuizDetail.razor` vzniklo minimum UI pro načtení detailu (`GET /api/quizzes/{quizId}`), upload/import CSV (`POST /api/quizzes/{quizId}/import-csv`) a zobrazení validačního reportu.
- Na stejné detail stránce bylo doplněno smazání kvízu (`DELETE /api/quizzes/{quizId}`) se zadáním mazacího hesla.
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
