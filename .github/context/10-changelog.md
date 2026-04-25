# Changelog

## 2026-05-01 — CH-04 HTTP API endpointy

Změny:
- Přidán `QuizApp.Server/Application/Challenges/ChallengeEndpoints.cs`.
- Endpointy: `GET /api/challenges/template`, `POST /api/challenges/`, `GET /api/challenges/{publicCode}`, `POST /api/challenges/{publicCode}/submissions`, `GET /api/challenges/{publicCode}/leaderboard`, `GET /api/challenges/{publicCode}/submissions/{submissionId}`.
- Endpointy zaregistrovány v `Program.cs` voláním `app.MapChallengeEndpoints()`.
- Rate limiting: vytvoření challenge `OrganizerMutations`, odeslání odpovědí `SubmitPerTeam`.
- Chybové odpovědi vracejí `ApiErrorResponse` se správnými HTTP status kódy (400/404).
- Build prošel.

---

## 2026-05-01 — CH-03 Challenge aplikační služba

Změny:
- Přidán `QuizApp.Server/Application/Challenges/ChallengeService.cs` s rozhraním `IChallengeService`.
- Přidán `QuizApp.Server/Application/Challenges/ChallengeTemplate.cs` s pevnou šablonou 10 otázek.
- `IChallengeService` zaregistrován v `Program.cs`.
- Přidány unit testy `QuizApp.Tests/ChallengeServiceTests.cs` (12 testů, všechny projšly).
- Správné odpovědi tvůrce nejsou předávány klientovi v `GetChallengeResponse`.
- Build prošel.

---

## 2026-05-01 — CH-02 Shared DTO a validační kontrakty

Změny:
- Přidán soubor `QuizApp.Shared/Contracts/ChallengeContracts.cs`.
- Obsahuje DTO: `GetChallengeTemplateResponse`, `CreateChallengeRequest/Response`, `GetChallengeResponse`, `SubmitChallengeAnswersRequest/Response`, `ChallengeLeaderboardResponse`, `GetChallengeSubmissionResultResponse` a pomocné DTO.
- `GetChallengeResponse` a `GetChallengeTemplateResponse` pro hraní neobsahují správné odpovědi.
- Build prošel.

---

## 2026-04-25 — CH-01 Datový model a EF Core migrace

Změny:
- Přidány entity: `Challenge`, `ChallengeQuestion`, `ChallengeAnswerOption`, `ChallengeSubmission`, `ChallengeSubmissionAnswer`.
- Přidána EF Core konfigurace v `QuizAppDbContext` (indexy, unikátní omezení, kaskádové mazání).
- Vygenerována migrace `AddChallengeMode` — pouze nové tabulky, bez zásahu do starých tabulek.
- Build prošel.

## 2026-04-25 — Reset vývojové dokumentace pro Challenge mód

Změny:
- Odstraněna většina staré roadmapy pro původní Pub kvíz MVP.
- Původní Pub kvíz aplikace označena jako hotová.
- Přidána nová specifikace virálního Challenge módu.
- Přidána nová roadmapa CH-01 až CH-09.
- Přidána pravidla proti nechtěné reimplementaci hotové aplikace.
- Přidány smoke testy pro Challenge MVP.
- Aktualizovány prompt soubory pro pokračování vývoje nové funkce.

Cíl:
- Umožnit AI agentovi ve Visual Studiu pokračovat vývojem nové virální funkce bez rozbíjení hotového Pub kvízu.
