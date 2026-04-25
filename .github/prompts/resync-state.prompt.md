---
agent: 'agent'
description: 'Znovu srovnej .github stavové soubory se skutečným stavem repozitáře'
---

Tvým cílem je sesynchronizovat `.github/context` se skutečným stavem kódu.

Nejdřív přečti:
- `.github/context/00-source-of-truth.md`
- `.github/context/04-roadmap.md`
- `.github/context/08-implementation-state.md`
- `.github/context/09-decision-log.md`
- `.github/context/10-changelog.md`

Pak:
1. analyzuj skutečný stav kódu,
2. urči, které Challenge kroky jsou reálně hotové,
3. oprav stavové soubory, aby odpovídaly realitě,
4. nevytvářej nové feature změny, pokud nejsou nutné pro konzistenci,
5. nemaž ani nepřepisuj existující Pub kvíz funkce,
6. vypiš jasně, co bylo ve stavových souborech upraveno.

Doplňující poznámka: ${input:note:Volitelná informace od uživatele}
