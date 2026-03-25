---
agent: 'agent'
description: 'Proveď přesně další nedokončený krok z roadmapy, aktualizuj stav a zastav se'
---

Pracuj jako implementační agent pro tento repozitář.

Nejdřív přečti:
- `.github/context/00-source-of-truth.md`
- `.github/context/04-roadmap.md`
- `.github/context/05-workflow-rules.md`
- `.github/context/08-implementation-state.md`
- `.github/context/09-decision-log.md`

Pak:
1. najdi první nedokončený krok,
2. proveď jen tento krok,
3. udělej build a relevantní testy,
4. aktualizuj:
   - `.github/context/08-implementation-state.md`
   - `.github/context/09-decision-log.md`
   - `.github/context/10-changelog.md`
5. vypiš stručný report v sekcích:
   - Hotový krok
   - Co bylo změněno
   - Ověření
   - Ruční kontrola pro uživatele
   - Stav

Volitelná poznámka od uživatele: ${input:note:Sem lze dopsat dodatečné upřesnění. Pokud nic není, ignoruj.}
