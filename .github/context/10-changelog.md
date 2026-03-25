# Changelog implementace

Tento soubor shrnuje, co AI v repozitáři změnila krok po kroku.

## Jak zapisovat
Pro každý dokončený krok přidej záznam ve formátu:

## SXX — Název kroku
- Co vzniklo
- Co bylo upraveno
- Jak bylo ověřeno
- Co případně zůstává jako známé omezení

---

## Záznamy

## S00 — Bootstrap repozitáře a solution
- Ověřena existující solution a přítomnost projektů `QuizApp.Client`, `QuizApp.Server`, `QuizApp.Shared`, `QuizApp.Tests`.
- Ověřeny projektové reference mezi `Server/Client -> Shared` a `Tests -> Server + Shared`.
- Ověřeno buildem solution a spuštěním základního testu `QuizApp.Tests.UnitTest1.Test1`.
- Omezení: ruční smoke-check otevření solution a zobrazení projektů v Solution Exploreru musí potvrdit uživatel v IDE.
