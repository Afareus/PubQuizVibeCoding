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

### D-006 — S01 bez DB health check závislého na provider balíčku
- **Datum/čas (UTC):** 2026-03-25T11:55:41Z
- **Krok:** S01
- **Rozhodnutí:** V kroku `S01` je použit obecný `health` endpoint přes `AddHealthChecks()` bez napojení na PostgreSQL-specific health check provider.
- **Důvod:** Cíl kroku je připravit hostingovou kostru, konfiguraci PostgreSQL a middleware bez předčasného rozšiřování závislostí.
- **Dopad:** Server je připravený na následné databázové kroky a endpoint `GET /health` vrací základní stav hostu.

### D-007 — S03 kontrakty navrženy jako transportní rekordy a enumy
- **Datum/čas (UTC):** 2026-03-25T12:54:41.9162834Z
- **Krok:** S03
- **Rozhodnutí:** Sdílené kontrakty v `QuizApp.Shared` jsou zavedeny jako jednoduché `record` DTO a enumy ve dvou oblastech (`Contracts`, `Enums`), včetně mapování `RealtimeEventName -> wire name`.
- **Důvod:** Krok S03 vyžaduje čitelné a minimální kontrakty bez předčasného uzamykání interní business logiky nebo persistence detailů.
- **Dopad:** Následující backendové kroky mají stabilní základ pro API a SignalR názvosloví bez zavádění nadbytečných závislostí.

### D-008 — S04 doménový model umístěn do serveru s explicitními guardy
- **Datum/čas (UTC):** 2026-03-25T13:40:00Z
- **Krok:** S04
- **Rozhodnutí:** Entitní model MVP je vytvořen v `QuizApp.Server/Domain/Entities` s privátními konstruktory, továrními metodami a jednoduchými guardy pro povinné hodnoty, UTC časy a základní rozsahy.
- **Důvod:** Krok S04 požaduje úplný doménový model a základní invariants bez předčasného zavádění persistence konfigurací.
- **Dopad:** Krok S05 může navázat čistým EF Core mapováním nad již stabilními entitami a navigacemi.
