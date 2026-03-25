# Workflow pravidla pro AI vývoj

## Cíl workflow
Umožnit vývoj stylem:
- AI udělá malý bezpečný krok,
- uživatel zkontroluje výsledek,
- uživatel napíše „Pokračuj k dalšímu kroku“.

## Povinný algoritmus pro každý implementační tah
1. Najdi první nedokončený krok v `08-implementation-state.md`.
2. Přečti plný popis kroku v `04-roadmap.md`.
3. Udělej minimální množství změn potřebných k dokončení právě tohoto kroku.
4. Proveď build.
5. Proveď relevantní testy.
6. Aktualizuj stavové soubory.
7. Vrať report a zastav se.

## Jak zabránit scope creepu
Před každou větší změnou si polož tyto otázky:
- Je to výslovně v MVP?
- Je to nezbytné pro aktuální krok?
- Nevytváří to širší produkt než specifikace?

Pokud je odpověď „ne“, změnu nedělej.

## Jak zabránit rozbití kontextu mezi kroky
Po každém kroku aktualizuj:
- `08-implementation-state.md`
- `09-decision-log.md`
- `10-changelog.md`

Tyto soubory slouží jako lokální paměť repozitáře.

## Kdy se nesmí pokračovat do dalšího kroku
Nepokračuj dál, když:
- build padá,
- krok není skutečně hotový,
- změny nejsou zapsané do stavových souborů,
- byly přidané dočasné hacky, které blokují další krok,
- není jasné, zda byl porušen zdroj pravdy.

## Jak řešit blokery
### Bloker typu A — malá nejasnost
Zvol nejjednodušší variantu konzistentní s MVP, zapiš ji do decision logu a pokračuj.

### Bloker typu B — rozbitý předchozí krok
Nejprve oprav předchozí krok.
Nepřeskakuj.

### Bloker typu C — rozpor ve zdroji pravdy
Zastav se, popiš rozpor a navrhni nejmenší bezpečnou variantu.
Nepokračuj do dalších feature změn.

## Požadovaná granularita změn
Dobrá změna:
- jedna oblast,
- jeden smysluplný cíl,
- snadno reviewovatelný diff.

Špatná změna:
- backend + frontend + refaktor + test infra najednou,
- skok přes více roadmap kroků,
- změna architektury bez nutnosti.

## Doporučený styl commitů
AI nemusí dělat commity, ale má se chovat tak, aby každé kolo odpovídalo jednomu rozumnému commitu.
