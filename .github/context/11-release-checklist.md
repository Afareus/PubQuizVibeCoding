# S21 — Release readiness checklist

Tento checklist uzavírá MVP do předatelného stavu pro další testování.

## 1) Build a test baseline
- [x] `run_build` pro celou solution úspěšný.
- [x] `run_tests` pro `QuizApp.Tests` úspěšný.
- [x] Kritické business unit testy pokrývají core pravidla (create/import, auth, start/cancel, submit, progression, results, tie-break, sanitizace).

## 2) Integrační testy s nejvyšší hodnotou
- [x] API E2E flow přes HTTP: vytvoření kvízu, import CSV, vytvoření session, join týmu, snapshot organizátora.
- [x] API auth invariant: týmový state endpoint vyžaduje `X-Team-Reconnect-Token` (missing/invalid/valid).
- [x] Test host běží izolovaně na InMemory DB (bez závislosti na lokálním PostgreSQL).

## 3) MVP scope kontrola
- [x] Nebyly přidány nové funkce mimo roadmapu.
- [x] Zachován modulární monolit a server-authoritative session engine.
- [x] Zachováno bezpečnostní minimum z předchozích kroků (hashing, constant-time compare, rate limiting, reconnect policy).

## 4) Známá omezení před produkčním nasazením
- [x] Ručně ověřit `dotnet ef database update` proti běžícímu PostgreSQL (ověřeno příkazem `dotnet dotnet-ef database update`).
- [ ] Projít ruční smoke test v reálném browser/SignalR prostředí (join/start/progression/results, rate limit/reconnect).

## 5) Stav předání
- [x] Roadmapa S00–S21 je implementačně uzavřena.
- [x] Stavové soubory (`08`, `09`, `10`) jsou aktualizované.
