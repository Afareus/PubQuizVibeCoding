# Zdroj pravdy projektu

## Aktuální stav

Původní Pub kvíz aplikace je považovaná za hotovou.

Tento repozitář už se nemá používat k opětovnému budování původního live Pub kvízu od nuly. Nový vývoj se soustředí na samostatný virální **Challenge mód**.

## Nový produktový cíl

Přidat do existující aplikace samostatný asynchronní režim:

```text
Kdo mě zná nejlíp?
```

Uživatel vytvoří osobní kvíz o sobě, získá veřejný odkaz, pošle ho přátelům a ti hádají jeho odpovědi. Po dokončení vidí skóre, žebříček a výzvu k vytvoření vlastního kvízu.

## Závazné vymezení Challenge MVP

- Webová aplikace v češtině.
- Challenge mód je oddělený od live Pub kvíz session.
- Challenge mód není live hra a nepotřebuje SignalR.
- Nepoužívá týmy.
- Nepoužívá organizátora během hry.
- Nepoužívá CSV import.
- Nepotřebuje registraci, login ani uživatelské účty.
- Vytvoření challenge musí být rychlé a mobilně pohodlné.
- Hráč se připojuje přes veřejný URL-safe kód v odkazu.
- Hráč zadá své jméno a vyplní odpovědi.
- Po dokončení se uloží výsledek a zobrazí se leaderboard.
- Na konci musí být výrazné CTA: **„Vytvořit vlastní kvíz“**.
- První verze používá pevnou šablonu 10 předpřipravených single-choice otázek.
- Každá otázka má přesně 4 možnosti A, B, C, D.
- Tvůrce challenge u každé otázky vybere svou odpověď.
- Hráči u každé otázky hádají odpověď tvůrce.
- Skóre: 1 bod za shodu, 0 bodů za neshodu.
- Maximum: 10 bodů.
- Leaderboard řadí primárně podle skóre sestupně, sekundárně podle času odeslání vzestupně.

## Co se nesmí rozbít

- Existující live Pub kvíz mód.
- Existující Organizer/Player flow.
- Existující databázové entity, pokud jejich změna není nutná.
- Existující CSV import.
- Existující SignalR session logika.
- Existující routy.
- Existující deployment konfigurace.
- Existující build.

## Technologie

- `.NET 8`
- `Blazor WebAssembly / Blazor Web App podle aktuálního stavu repo`
- `ASP.NET Core backend`
- `PostgreSQL`
- `Entity Framework Core`
- existující architektura: modulární monolit

## Zdroj pravdy při konfliktu

- Pro již existující Pub kvíz chování je zdrojem pravdy skutečný kód.
- Pro nový Challenge mód je zdrojem pravdy tato `.github/context` dokumentace.
- Pokud dokumentace o starém Pub kvízu neodpovídá kódu, neměň kvůli tomu starý kód. Maximálně aktualizuj dokumentaci.
