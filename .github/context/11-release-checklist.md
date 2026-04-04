# S21 — Release readiness checklist

Tento checklist uzavírá MVP do předatelného stavu pro další testování.

## 1) Build a test baseline
- [x] `run_build` pro celou solution úspěšný.
- [x] `run_tests` pro `QuizApp.Tests` úspěšný (124/124).
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

---

## 6) Reconnect hardening — test matrix (R11)

### 6.1) Automatizované testy (R09)
- [x] 11 unit testů: snapshot verze, deduplikace `ClientRequestId`, reconnect state machine, presence přechody, plný reconnect cyklus.
- [x] 8 API integračních testů: heartbeat (tým/organizátor), reconnect po startu/cancelu, idempotentní HTTP submit, organizátor snapshot metadata.
- [x] Celkový stav: 124/124 testů prochází stabilně.

### 6.2) E2E smoke testy — manuální matrice
Podrobné scénáře viz `.github/context/12-reconnect-e2e-smoke-tests.md`.

#### Platformy k ověření
| # | Platforma | Prohlížeč | Stav |
|---|-----------|-----------|------|
| 1 | Windows desktop | Chrome (latest) | [ ] |
| 2 | Windows desktop | Firefox (latest) | [ ] |
| 3 | Windows desktop | Edge (latest) | [ ] |
| 4 | macOS desktop | Safari (latest) | [ ] |
| 5 | Android mobilní | Chrome | [ ] |
| 6 | iOS mobilní | Safari | [ ] |

#### Síťové podmínky k simulaci
| # | Podmínka | DevTools profil | Scénář |
|---|----------|-----------------|--------|
| 1 | Krátkodobý výpadek (5 s) | Offline toggle | Scénář 1 |
| 2 | Střednědobý výpadek (30 s) | Offline toggle | Scénář 2 |
| 3 | Dlouhodobý výpadek (90+ s) | Offline toggle | Scénář 3 |
| 4 | Pomalá síť (Slow 3G) | Network throttling | Odpovědi + reconnect |
| 5 | Nestabilní spojení (Fast 3G + packet loss) | Network throttling | Submit retry |
| 6 | Tab sleep / pozastavení | Přepnutí aplikace/tabu | Scénář 10 |
| 7 | Page reload (F5) | — | Scénáře 4, 7 |

#### Kritické scénáře k ověření na každé platformě
- [ ] **Tým: reconnect po výpadku** — klient zobrazí Reconnecting, po obnovení Resynced, odpočet se přepočítá.
- [ ] **Tým: reload tabu** — bootstrap `/tym/obnovit/{sessionId}` naviguje na správnou obrazovku.
- [ ] **Tým: submit s výpadkem** — pending submit se automaticky retryuje, server deduplikuje přes `ClientRequestId`.
- [ ] **Tým: session zrušena během offline** — po obnovení redirect na hlavní stránku.
- [ ] **Tým: více týmů v jednom browseru** — nezávislé identity a odpovědi.
- [ ] **Organizátor: reconnect po výpadku** — snapshot se obnoví, Start/Cancel disabled během in-flight.
- [ ] **Organizátor: reload tabu** — banner „Obnovit řízení session" na detailu kvízu.
- [ ] **Přesné časování** — drift korekce ze `ServerUtcNow`, deadline guard po uplynutí času.
- [ ] **Fallback poll** — při selhání SignalR klient přepne na REST poll (3 s interval).

### 6.3) Reconnect hardening — ověření kroků R01–R10

| Krok | Popis | Build | Testy | Stav |
|------|-------|-------|-------|------|
| R01 | Specifikace reconnect stavů a UX contract | ✅ | N/A (dokumentace) | Hotovo |
| R02 | Server-side přítomnost a heartbeat | ✅ | ✅ (presence testy) | Hotovo |
| R03 | Verze snapshotu a deterministická resync | ✅ | ✅ (snapshot version testy) | Hotovo |
| R04 | Realtime odolnost: subscribe ack + fallback poll | ✅ | ✅ (4 cílené testy) | Hotovo |
| R05 | Idempotentní submit odpovědí | ✅ | ✅ (dedup test) | Hotovo |
| R06 | Team flow: návrat po reloadu/restartu | ✅ | N/A (klientská logika) | Hotovo |
| R07 | Organizer flow: návrat do aktivní session | ✅ | N/A (klientská logika) | Hotovo |
| R08 | Přesné časování při reconnectu (drift korekce) | ✅ | N/A (klientská logika) | Hotovo |
| R09 | Testy odolnosti (unit + integrační + E2E scénáře) | ✅ | ✅ (124/124) | Hotovo |
| R10 | Provozní observabilita reconnectu | ✅ | ✅ (124/124) | Hotovo |
| R11 | Release hardening checklist | ✅ | ✅ (124/124) | Hotovo |

---

## 7) Rollback plán pro realtime vrstvu

Pokud se po nasazení projeví regrese v realtime/reconnect vrstvě:

### 7.1) Detekce problému
- Monitorovat diagnostický endpoint `GET /api/diagnostics/reconnect-metrics`:
  - `FailedResyncCount > 50` za 5 min → alert.
  - `TeamReconnectCount > 200` za 5 min → neobvyklá míra odpojení.
  - `DuplicateSubmitRetryCount > 30` za 5 min → síťové problémy klientů.
  - `AverageResyncDurationMs > 5000` → pomalý resync.
- Strukturované logy s `SessionId`, `TeamId`, `ConnectionId`, `SnapshotVersion` umožňují rychlou diagnostiku.

### 7.2) Eskalační kroky
1. **Úroveň 1 — Zvýšený monitoring**: resetovat metriky přes `POST /api/diagnostics/reconnect-metrics/reset`, sledovat nový 5min okno.
2. **Úroveň 2 — Fallback na REST-only režim**: klienti mají vestavěný fallback poll (3 s). Pokud je SignalR hub nestabilní, dočasně odstavit `/hubs/sessions` endpoint (klienti automaticky přejdou na poll).
3. **Úroveň 3 — Rollback nasazení**: vrátit předchozí verzi deployment artefaktu (Docker image tag / Railway deployment). Session data v PostgreSQL zůstanou konzistentní díky server-authoritative modelu.

### 7.3) Bezpečnostní záruky
- Server je jediná autorita nad stavem session → rollback serveru nezpůsobí nekonzistenci klientských dat.
- Klientský fallback poll zajistí funkčnost i při úplném výpadku SignalR.
- `ClientRequestId` deduplikace chrání proti duplicitním submitům i při nestabilním spojení.
- Heartbeat presence (`Connected` / `TemporarilyDisconnected` / `Inactive`) nemá vliv na skórování — rollback presence logiky nezpůsobí ztrátu bodů.
