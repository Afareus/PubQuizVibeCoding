# Decision log

Tento soubor obsahuje pouze rozhodnutí relevantní pro další vývoj po dokončení původní Pub kvíz aplikace.

## 2026-04-25 — Původní Pub kvíz aplikace je považovaná za hotovou

Rozhodnutí:
- Neudržovat v `.github/context` dlouhou roadmapu původního MVP.
- Nechat existující kód jako zdroj pravdy pro starý Pub kvíz mód.
- Další vývoj soustředit na nový Challenge mód.

Důvod:
- Původní aplikace už je hotová.
- Staré instrukce by mohly vést AI agenta k reimplementaci hotových částí.

## 2026-04-25 — Challenge mód bude samostatný asynchronní modul

Rozhodnutí:
- Challenge mód nebude live session.
- Nebude používat SignalR.
- Nebude používat týmy.
- Nebude vyžadovat organizátora během hry.

Důvod:
- Cílem je jednoduché virální sdílení přes odkaz.
- Čím menší friction, tím vyšší šance na šíření.

## 2026-04-25 — První Challenge MVP používá pevnou šablonu 10 otázek

Rozhodnutí:
- První verze nebude mít vlastní otázky.
- První verze nebude mít AI generování.
- Tvůrce jen vybere svoje odpovědi na 10 předpřipravených otázek.

Důvod:
- Rychlé vytvoření challenge.
- Menší riziko nevhodného obsahu.
- Jednodušší implementace.
- Lepší kontrola kvality UX.

## 2026-04-25 — Virální CTA je součást MVP

Rozhodnutí:
- Výsledková stránka musí obsahovat výrazné tlačítko `Vytvořit vlastní kvíz`.
- Bez tohoto CTA není virální smyčka dokončená.

Důvod:
- Cílem není jen odehrání jednoho kvízu.
- Cílem je šíření: hráč se má stát dalším tvůrcem.

## 2026-04-25 — Správné odpovědi tvůrce se nesmí posílat před submission

Rozhodnutí:
- GET challenge pro hráče nesmí obsahovat tvůrcovy odpovědi.
- Skórování probíhá na serveru.

Důvod:
- Jinak by si hráč mohl odpovědi zobrazit v DevTools nebo síťové komunikaci.
