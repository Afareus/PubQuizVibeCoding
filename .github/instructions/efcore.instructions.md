---
applyTo: "**/*DbContext*.cs,**/*EntityTypeConfiguration*.cs,**/Migrations/**/*.cs"
---
# Pravidla pro EF Core a databázi

- Používej PostgreSQL kompatibilní mapování.
- Nepoužívej lazy loading.
- Explicitně nastav indexy a unikátní omezení podle business pravidel.
- Migrace udržuj čisté a čitelné.
- Při přidání Challenge entit zkontroluj, že migrace neobsahuje nečekané zásahy do starých Pub kvíz tabulek.
- `Challenge.PublicCode` musí být unikátní.
- `ChallengeQuestion` má unikátní kombinaci `ChallengeId + OrderIndex`.
- `ChallengeAnswerOption` má unikátní kombinaci `ChallengeQuestionId + OptionKey`.
- `ChallengeSubmissionAnswer` má unikátní kombinaci `ChallengeSubmissionId + ChallengeQuestionId`.
- Hashovaná pole nevracej do DTO a dimenzuj je bezpečně.
