---
applyTo: "**/*DbContext*.cs,**/*EntityTypeConfiguration*.cs,**/Migrations/**/*.cs"
---
# Pravidla pro EF Core a databázi

- Používej PostgreSQL kompatibilní mapování.
- Nepoužívej lazy loading.
- Explicitně nastav indexy a unikátní omezení podle business pravidel.
- Pro `QuizSession` nastav optimistickou concurrency čitelným způsobem.
- Hashovaná pole dimenzuj tak, aby bezpečně pojala zvolený hash.
- Logické smazání kvízu řeš explicitními poli, ne fyzickým mazáním.
- Migrace udržuj čisté a čitelné.
- Do migrací nepatří nahodilé ruční zásahy, které nejsou vysvětlené modelem.
