---
agent: 'agent'
description: 'Proveď přesně další nedokončený krok Challenge roadmapy, aktualizuj stav a zastav se'
---

Pracuj jako implementační agent pro tento repozitář.

Nejdřív přečti:
- `.github/context/00-source-of-truth.md`
- `.github/context/01-product-spec.md`
- `.github/context/04-roadmap.md`
- `.github/context/05-workflow-rules.md`
- `.github/context/08-implementation-state.md`

Pak:
1. najdi první nedokončený krok v Challenge roadmapě,
2. proveď pouze tento krok,
3. nerozbíjej hotový Pub kvíz mód,
4. udělej build a relevantní testy,
5. aktualizuj:
   - `.github/context/08-implementation-state.md`
   - `.github/context/09-decision-log.md`, pokud padlo rozhodnutí
   - `.github/context/10-changelog.md`
6. vypiš stručný report v sekcích:
   - Hotový krok
   - Co bylo změněno
   - Ověření
   - Ruční kontrola pro uživatele
   - Další krok

Volitelná poznámka od uživatele: ${input:note:Sem lze dopsat dodatečné upřesnění. Pokud nic není, ignoruj.}
