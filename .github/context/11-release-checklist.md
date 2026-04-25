# Release checklist pro Challenge MVP

## Build a testy

- [ ] Backend build projde.
- [ ] Client build projde.
- [ ] Testy projdou, pokud existují.
- [ ] EF Core migrace je čistá a neobsahuje nečekané změny starých tabulek.

## Funkční kontrola

- [ ] Lze otevřít `/challenge/create`.
- [ ] Lze vytvořit challenge.
- [ ] Vytvoření vrátí veřejný odkaz.
- [ ] Veřejný odkaz lze otevřít v jiném prohlížeči.
- [ ] Hráč může zadat jméno.
- [ ] Hráč může odpovědět na 10 otázek.
- [ ] Hráč vidí skóre.
- [ ] Leaderboard zobrazuje výsledky.
- [ ] CTA `Vytvořit vlastní kvíz` vede na tvorbu nové challenge.

## Bezpečnostní kontrola

- [ ] Detail challenge pro hráče neobsahuje správné odpovědi.
- [ ] API nevrací hashovaná pole.
- [ ] Vstupy mají délkové limity.
- [ ] Neexistující public code vrací srozumitelnou chybu.

## Kontrola původní aplikace

- [ ] Existující Pub kvíz hlavní flow je stále dostupný.
- [ ] Existující Organizer/Player navigace není odstraněná.
- [ ] Existující SignalR/live session kód není zjevně rozbitý.

## UX kontrola

- [ ] Create flow je použitelný na mobilu.
- [ ] Play flow je použitelný na mobilu.
- [ ] Texty jsou česky.
- [ ] Sdílecí text je krátký a pochopitelný.
- [ ] Výsledková stránka dokončuje virální smyčku.
