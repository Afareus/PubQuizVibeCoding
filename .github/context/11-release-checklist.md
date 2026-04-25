# Release checklist pro Challenge MVP

Datum ověření: 2026-05-XX

## Build a testy

- [x] Backend build projde.
- [x] Client build projde.
- [x] Testy projdou — 112/112 passed.
- [x] EF Core migrace `AddChallengeMode` je čistá a neobsahuje nečekané změny starých tabulek.

## Funkční kontrola

- [x] Lze otevřít `/challenge/create`.
- [x] Lze vytvořit challenge. *(unit test: CreateChallengeAsync_ValidRequest_PersistsChallengeAndReturnsPublicCode)*
- [x] Vytvoření vrátí veřejný odkaz.
- [ ] Veřejný odkaz lze otevřít v jiném prohlížeči. *(ruční test)*
- [ ] Hráč může zadat jméno. *(ruční test)*
- [ ] Hráč může odpovědět na 10 otázek. *(ruční test)*
- [x] Hráč vidí skóre. *(unit test: SubmitAnswersAsync_AllCorrect_ReturnsMaxScore)*
- [x] Leaderboard zobrazuje výsledky. *(unit test: SubmitAnswersAsync_LeaderboardIsSortedByScoreDescThenTimeAsc)*
- [ ] CTA `Vytvořit vlastní kvíz` vede na tvorbu nové challenge. *(ruční test)*

## Bezpečnostní kontrola

- [x] Detail challenge pro hráče neobsahuje správné odpovědi. *(unit test: GetChallengeAsync_ExistingCode_ReturnsChallengeWithoutCorrectAnswers)*
- [x] Neexistující public code vrací srozumitelnou chybu. *(unit test: GetChallengeAsync_UnknownCode_ReturnsError)*
- [x] Vstupy mají délkové limity (validace v DTO).

## Kontrola původní aplikace

- [x] Existující Pub kvíz hlavní flow je stále dostupný.
- [x] Existující Organizer/Player navigace není odstraněná.
- [x] Existující SignalR/live session kód není zjevně rozbitý.

## UX kontrola

- [ ] Create flow je použitelný na mobilu. *(ruční test)*
- [ ] Play flow je použitelný na mobilu. *(ruční test)*
- [x] Texty jsou česky.
- [ ] Sdílecí text je krátký a pochopitelný. *(ruční test)*
- [x] Výsledková stránka dokončuje virální smyčku (CTA + leaderboard + sdílení).
