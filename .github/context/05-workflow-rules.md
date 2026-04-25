# Workflow pravidla pro AI agenta

## 1. Hlavní pravidlo

Původní Pub kvíz aplikace je hotová.

Agent má pokračovat výhradně na nové funkci **Challenge mód**, pokud uživatel výslovně neřekne jinak.

## 2. Před každým krokem

Přečti:
- `.github/context/00-source-of-truth.md`
- `.github/context/04-roadmap.md`
- `.github/context/08-implementation-state.md`
- relevantní specifikační soubory podle kroku

Pak:
1. najdi první nedokončený krok,
2. proveď pouze tento krok,
3. nepřeskakuj roadmapu,
4. neimplementuj více kroků najednou,
5. po dokončení aktualizuj stav.

## 3. Co musíš chránit

- existující live Pub kvíz mód,
- existující Organizer/Player flow,
- existující databázové tabulky, pokud jejich změna není nutná,
- existující CSV import,
- existující SignalR logiku,
- existující deployment konfiguraci,
- aktuální styl projektu.

## 4. Co bez výslovného pokynu nedělat

- nepřepisuj hotovou aplikaci od nuly,
- nepřidávej login ani Identity,
- nepřidávej vlastní otázky pro Challenge MVP,
- nepřidávej AI generování otázek,
- nepřidávej platby,
- nepřidávej reklamy,
- nepřidávej globální katalog challenge,
- nepředělávej architekturu na mikroservisy,
- neměň databázi,
- neměň framework,
- nereorganizuj celý projekt kvůli estetice,
- neskrývej nehotový krok jako hotový.

## 5. Styl implementace

- Preferuj jednoduché a čitelné řešení.
- Drž se existujícího stylu kódu.
- Server je autorita pro scoring.
- Časy ukládej v UTC.
- Endpointy drž tenké.
- Business pravidla dávej do služeb.
- DTO nesmí nechtěně vracet správné odpovědi.
- Nepoužívej složité patterny bez jasného důvodu.

## 6. Definition of Done pro každý krok

Krok je hotový jen tehdy, když:
- odpovídá specifikaci,
- build projde nebo je přesně uvedeno, proč nešel spustit,
- relevantní testy projdou nebo je uvedeno, proč nejsou dostupné,
- ruční smoke-check je popsán,
- `08-implementation-state.md` je aktualizovaný,
- `09-decision-log.md` je aktualizovaný, pokud padlo rozhodnutí,
- `10-changelog.md` je aktualizovaný.

## 7. Report po kroku

Na konci vypiš:
- Hotový krok
- Co bylo změněno
- Ověření
- Ruční kontrola pro uživatele
- Stav dalšího kroku
