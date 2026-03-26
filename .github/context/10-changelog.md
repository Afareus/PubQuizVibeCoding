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
