# Stav implementace

Tento soubor je provozní paměť repozitáře.
Po každém kroku jej aktualizuj.

## Jak aktualizovat
- Označ dokončený krok `[x]`.
- U dalšího kroku ponech `[ ]`.
- Aktualizuj sekci „Naposledy dokončeno“.
- Aktualizuj sekci „Aktuální poznámky“.
- Aktualizuj sekci „Rizika / dluh“ jen pokud skutečně něco zůstává.

## Roadmap status
- [x] S00 — Bootstrap repozitáře a solution
- [ ] S01 — Základ hostingu a konfigurace serveru
- [ ] S02 — Základ klienta a routingu
- [ ] S03 — Sdílené kontrakty a enumy
- [ ] S04 — Entitní model domény
- [ ] S05 — EF Core mapování a DbContext
- [ ] S06 — První migrace a databázový bootstrap
- [ ] S07 — CSV kontrakt, parser a validační report
- [ ] S08 — Služba pro založení kvízu a import otázek
- [ ] S09 — REST endpointy pro správu kvízů
- [ ] S10 — Organizátorské UI pro kvízy
- [ ] S11 — Session create backend a join code
- [ ] S12 — Team join backend a reconnect identita
- [ ] S13 — Organizátorský waiting room a session create UI
- [ ] S14 — Start/cancel session backend
- [ ] S15 — Otázkový engine a timeout progression
- [ ] S16 — SignalR session groups a eventy
- [ ] S17 — Team UI: join, waiting room, question screen
- [ ] S18 — Answer submit backend
- [ ] S19 — Výsledky, ranking a correct answers
- [ ] S20 — Hardening a bezpečnostní minimum
- [ ] S21 — Testy a release readiness

## Naposledy dokončeno
- S00 — Bootstrap repozitáře a solution (ověřeno 2026-03-25 UTC).

## Aktuální poznámky
- Solution `QuizApp.sln` existuje a obsahuje projekty `QuizApp.Client`, `QuizApp.Server`, `QuizApp.Shared`, `QuizApp.Tests`.
- Ověřeno, že reference mezi projekty odpovídají roadmapě pro `S00` a solution buildí.
- Další krok je `S01`.

## Rizika / dluh
- Zatím žádný evidovaný technický dluh.

## Poslední ověření
- Build: úspěšný (`run_build`)
- Testy: úspěšné (`QuizApp.Tests.UnitTest1.Test1`)
- Ruční smoke check: neproběhl (vyžaduje ruční kontrolu v IDE)
