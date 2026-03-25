# Decision log

Zapisuj sem všechna doplněná rozhodnutí, která nebyla explicitně daná ve zdroji pravdy, ale byla zvolena proto, aby mohl vývoj plynule pokračovat.

## Pravidla zápisu
Každý záznam má mít:
- datum/čas v UTC, pokud je k dispozici,
- krátký identifikátor kroku,
- rozhodnutí,
- důvod,
- dopad.

---

## Počáteční doplněná rozhodnutí této workflow sady

### D-001 — Jemnější roadmapa než v původní analýze
- **Rozhodnutí:** Původních 9 fází bylo rozpadnuto do kroků S00–S21.
- **Důvod:** Umožňuje stabilní AI workflow po malých krocích s jednoduchým příkazem „Pokračuj k dalšímu kroku“.
- **Dopad:** Menší diffy, jednodušší review, nižší riziko driftu.

### D-002 — Přidán projekt `QuizApp.Tests`
- **Rozhodnutí:** Testy budou odděleny do samostatného projektu `QuizApp.Tests`.
- **Důvod:** Původní analýza explicitně nepojmenovala test projekt, ale automatické testy jsou potřeba pro bezpečný AI vývoj.
- **Dopad:** Stabilnější verifikace business pravidel.

### D-003 — Stavové soubory uvnitř `.github/context`
- **Rozhodnutí:** Stav implementace, decision log a changelog jsou uloženy v `.github/context`.
- **Důvod:** AI je má mít v jednom kontextovém prostoru s instrukcemi a roadmapou.
- **Dopad:** Lepší kontinuita mezi kroky.

### D-004 — Prompt files jsou pomocné, ne kritické
- **Rozhodnutí:** Workflow je navrženo tak, aby fungovalo i bez prompt files.
- **Důvod:** Prompt files jsou preview funkce.
- **Dopad:** Hlavní logika je v repo-wide instrukcích a stavových souborech.

---

## Další záznamy
Sem přidávej další rozhodnutí průběžně.

### D-005 — S00 přijato jako splněné na existující solution
- **Datum/čas (UTC):** 2026-03-25T11:08:22Z
- **Krok:** S00
- **Rozhodnutí:** Krok `S00` byl dokončen verifikací již existující solution a projektové kostry místo znovuvytváření projektů.
- **Důvod:** Uživatel explicitně uvedl, že prázdná solution je již založená.
- **Dopad:** Změny v tomto kroku jsou minimální a reverzibilní; pokračuje se na `S01`.
