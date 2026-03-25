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
