# Architektura a datový model

## Architektonické principy
- Server je autorita nad session stavem, pořadím otázek a časem.
- Klient dopočítává countdown lokálně z `QuestionStartedAtUtc` a `TimeLimitSec`.
- Real-time vrstva pouze synchronizuje změny; neposílá timer tick každou sekundu.
- Řešení je modulární monolit, ne mikroservisy.
- Doména, API a persistence mají být oddělené logickými vrstvami.
- Celé řešení musí být připravené na více současných session.

## Doporučené rozdělení solution
- `QuizApp.Client` – Blazor UI
- `QuizApp.Server` – ASP.NET Core API, SignalR, aplikační logika
- `QuizApp.Shared` – sdílené DTO, kontrakty, enumy
- `QuizApp.Tests` – automatické testy (doplněný návrh pro tento repozitář)

## Minimální entitní model

### Quiz
- `QuizId`
- `Name`
- `DeletePasswordHash`
- `QuizOrganizerTokenHash`
- `CreatedAtUtc`
- `DeletedAtUtc`
- `IsDeleted`

### Question
- `QuestionId`
- `QuizId`
- `OrderIndex`
- `Text`
- `TimeLimitSec`
- `CorrectOption`

### QuestionOption
- `QuestionOptionId`
- `QuestionId`
- `OptionKey`
- `Text`

### QuizSession
- `SessionId`
- `QuizId`
- `JoinCode`
- `Status`
- `CreatedAtUtc`
- `StartedAtUtc`
- `QuestionDeadlineUtc`
- `ConcurrencyToken`
- `FinishedAtUtc`
- `EndedAtUtc`
- `CurrentQuestionIndex`
- `CurrentQuestionStartedAtUtc`

### Team
- `TeamId`
- `SessionId`
- `Name`
- `NormalizedTeamName`
- `JoinedAtUtc`
- `LastSeenAtUtc`
- `TeamReconnectTokenHash`
- `Status`

### TeamAnswer
- `TeamAnswerId`
- `SessionId`
- `TeamId`
- `QuestionId`
- `SelectedOption`
- `SubmittedAtUtc`
- `IsCorrect`
- `ResponseTimeMs`

### SessionResult
- `SessionResultId`
- `SessionId`
- `TeamId`
- `Score`
- `CorrectCount`
- `TotalCorrectResponseTimeMs`
- `Rank`

### AuditLog
- `AuditLogId`
- `OccurredAtUtc`
- `ActionType`
- `QuizId`
- `SessionId`
- `PayloadJson`

## Databázové a modelové zásady
- Všechna datum/čas pole ukládej v UTC.
- Definuj unikátní omezení tam, kde to vyplývá z pravidel:
  - `JoinCode` pro aktivní session musí být prakticky unikátní,
  - `NormalizedTeamName` musí být unikátní v rámci jedné session,
  - odpověď týmu musí být unikátní pro kombinaci `SessionId + TeamId + QuestionId`.
- Nepoužívej lazy loading.
- Pro session mutace použij optimistickou concurrency nad `QuizSession`.
- Concurrency řeš provider-agnosticky a srozumitelně.
- Hashované hodnoty nikdy neposílej do DTO.

## Doplněné implementační preference pro tento repozitář
Tyto body nejsou přímo v původní analýze, ale byly doplněny proto, aby AI vývoj zůstal stabilní:
- testovací projekt bude `xUnit`,
- preferujeme čisté služby a jednoduché repository/DbContext použití bez MediatR a CQRS,
- pro pořadí kroků se drž vždy `context/04-roadmap.md`.
