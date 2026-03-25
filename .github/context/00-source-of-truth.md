# Zdroj pravdy projektu

Tento repozitář implementuje úzké MVP webové aplikace pro pub kvízy.

## Závazný produktový rámec
- Webová aplikace v češtině.
- Podpora více současně běžících session.
- V jeden okamžik právě jeden aktivní klient na tým.
- Identitu týmu lze převzít přes `TeamReconnectToken`.
- V jedné session maximálně 20 týmů.
- V MVP existuje pouze **single-choice** otázka:
  - přesně 4 možnosti: A, B, C, D,
  - přesně 1 správná odpověď,
  - časový limit 10 až 300 sekund.
- Import otázek pouze přes CSV.
- V UI není editace otázek.
- Nový kvíz se založí jako prázdný a pak se do něj jednorázově nahraje CSV.
- Výsledky se během hry nezobrazují.
- Správné odpovědi se během hry nezobrazují ani organizátorovi.
- Po skončení session vidí:
  - tým finální pořadí,
  - organizátor finální výsledky i správné odpovědi.
- Po startu session se nové týmy nepřipojují.
- Session lze zrušit jen po potvrzovacím dialogu.
- Kvíz lze logicky smazat jen s platným organizer tokenem a správným mazacím heslem.
- Kvíz nelze smazat, pokud existuje aktivní WAITING nebo RUNNING session.
- Neexistují uživatelské účty ani klasický login.

## Technologie
- `.NET 8`
- `Blazor Web App / klient ve WebAssembly režimu`
- `ASP.NET Core backend`
- `PostgreSQL`
- `SignalR`
- `Entity Framework Core`
- architektura: **modulární monolit**

## Co je mimo scope
- uživatelské účty, registrace, login, role-based access
- editace otázek v UI
- otevřené odpovědi
- multi-choice
- obrázky, audio, video
- průběžný leaderboard
- mezivýsledky mezi otázkami
- vyhazování nebo přejmenovávání týmů
- vícejazyčnost
- globální serverový katalog kvízů
- obnova ztraceného organizer tokenu

## Bezpečnostní minimum
- `QuizOrganizerToken` musí mít minimálně 256 bitů entropie.
- Na serveru se ukládá pouze hash tokenu.
- Mazací heslo se ukládá pouze jako hash.
- Porovnání tokenů musí být constant-time.
- Organizátorské requesty nesmí projít bez `X-Organizer-Token`.
- Týmové requesty po joinu nesmí projít bez `X-Team-Reconnect-Token`.

## Stavový model session
- `WAITING`
- `RUNNING`
- `FINISHED`
- `CANCELLED`

## Nejdůležitější implementační mantra
MVP je záměrně úzké.
Když něco není výslovně povoleno, ber to jako mimo scope.
