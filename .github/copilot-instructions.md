# Copilot/Codex instrukce pro tento repozitář

## Kontext

Tento repozitář obsahuje hotovou Pub kvíz aplikaci. Nyní se pokračuje novou funkcí:

```text
Virální Challenge mód „Kdo mě zná nejlíp?“
```

Nejsi ve fázi budování původní aplikace od nuly.

## Priorita zdrojů

1. Skutečný kód je zdroj pravdy pro existující Pub kvíz funkce.
2. `.github/context/00-source-of-truth.md` je zdroj pravdy pro nový směr.
3. `.github/context/01-product-spec.md` je produktová specifikace Challenge módu.
4. `.github/context/04-roadmap.md` určuje pořadí kroků.
5. `.github/context/08-implementation-state.md` říká aktuální stav.

## Hlavní pravidlo

Implementuj pouze první nedokončený krok z roadmapy.

Nepřidávej více funkcí najednou. Po dokončení kroku se zastav, zapiš stav a vypiš report.

## Co máš implementovat

Challenge mód:
- `/challenge/create`
- `/challenge/{publicCode}`
- výsledek + leaderboard
- sdílení odkazu
- CTA `Vytvořit vlastní kvíz`

Podrobnosti jsou v `.github/context`.

## Co máš chránit

Nerozbíjej:
- live Pub kvíz mód,
- Organizer/Player flow,
- CSV import,
- SignalR session logiku,
- existující deployment,
- existující routy a API,
- existující datový model, pokud změna není nutná.

## Co nemáš dělat bez výslovného zadání

- nereimplementuj Pub kvíz od nuly,
- nereorganizuj celý projekt,
- nepřidávej login, Identity, účty ani role,
- nepřidávej AI generování,
- nepřidávej vlastní otázky,
- nepřidávej platby,
- nepřidávej reklamy,
- nepřidávej veřejný katalog,
- nepřidávej sociální login,
- nepřidávej SignalR do Challenge módu.

## Implementační styl

- Drž se aktuálního stylu repozitáře.
- Preferuj jednoduchost.
- Business pravidla dávej do služeb.
- Endpointy drž tenké.
- DTO používej tak, aby neunikaly interní entity.
- Časy ukládej v UTC.
- Neposílej správné odpovědi challenge před odesláním submission.
- Při změně databáze kontroluj migraci, aby neobsahovala nečekané zásahy do starých tabulek.

## Povinný postup při `/continue` nebo „Pokračuj k dalšímu kroku“

1. Přečti:
   - `.github/context/00-source-of-truth.md`
   - `.github/context/04-roadmap.md`
   - `.github/context/05-workflow-rules.md`
   - `.github/context/08-implementation-state.md`
2. Najdi první nedokončený krok.
3. Proveď pouze tento krok.
4. Spusť build a relevantní testy, pokud to prostředí umožňuje.
5. Aktualizuj:
   - `.github/context/08-implementation-state.md`
   - `.github/context/09-decision-log.md`, pokud padlo rozhodnutí
   - `.github/context/10-changelog.md`
6. Vypiš report:
   - Hotový krok
   - Co bylo změněno
   - Ověření
   - Ruční kontrola pro uživatele
   - Další krok

## Když něco nejde ověřit

Nepiš, že je vše hotové bez ověření.

Napiš přesně:
- který příkaz nešel spustit,
- proč,
- co bylo ověřeno jinak,
- jaký ruční test má uživatel udělat.

## Když najdeš rozpor

Pokud je rozpor mezi dokumentací starého Pub kvízu a kódem:
- neměň starý kód jen kvůli dokumentaci,
- ber kód jako zdroj pravdy,
- případně oprav dokumentaci.

Pokud je rozpor v nové Challenge specifikaci:
- zastav se u nejmenší bezpečné interpretace,
- drž se `00-source-of-truth.md`,
- neexpanduj scope.
