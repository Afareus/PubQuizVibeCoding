# Changelog

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
