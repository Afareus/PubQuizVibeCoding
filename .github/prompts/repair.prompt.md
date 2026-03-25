---
agent: 'agent'
description: 'Oprav rozbitý stav bez scope creepu a vrať projekt do stavu, kdy lze pokračovat dalším krokem'
---

Tvým cílem je opravit aktuálně rozbitý stav a neodbočit do nové feature práce.

Nejdřív přečti:
- `.github/context/00-source-of-truth.md`
- `.github/context/05-workflow-rules.md`
- `.github/context/06-risks-and-anti-drift.md`
- `.github/context/08-implementation-state.md`

Pak:
1. identifikuj, co přesně je rozbité,
2. oprav jen minimum nutné k návratu do konzistentního stavu,
3. proveď build a relevantní testy,
4. neimplementuj další roadmap krok,
5. aktualizuj changelog a stavový soubor.

Doplňující poznámka: ${input:note:Volitelný popis chyby od uživatele}
