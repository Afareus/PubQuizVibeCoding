# E2E Smoke testy — reconnect odolnost (R09)

Tyto scénáře vyžadují manuální ověření v reálném prohlížečovém prostředí se SignalR.
Označuj každý scénář `[x]` po úspěšném ověření.

## Předpoklady
- Server běží a je připojený k PostgreSQL.
- Klient je dostupný v browseru.
- K dispozici je DevTools (F12) pro simulaci offline režimu a Network throttling.

---

## Scénář 1 — Výpadek sítě 5 s (tým)
- [ ] Tým se připojí do session, organizátor spustí hru.
- [ ] Tým vidí otázku s odpočtem.
- [ ] V DevTools přepni do offline režimu na 5 s, poté obnovit.
- [ ] Klient zobrazí stav „Reconnecting…" a po obnovení „Online".
- [ ] Odpočet se po reconnectu přepočítá ze serverového snapshotu.
- [ ] Tým může odeslat odpověď bez ztráty kontextu.

## Scénář 2 — Výpadek sítě 30 s (tým)
- [ ] Stejné kroky jako scénář 1, ale offline trvá 30 s.
- [ ] Po reconnectu se klient přepne do fallback REST poll režimu.
- [ ] Po obnovení SignalR se vrátí do realtime režimu.
- [ ] Pokud mezitím proběhla progrese na další otázku, klient zobrazí novou otázku.

## Scénář 3 — Výpadek sítě 90+ s (tým)
- [ ] Offline přes 90 s, poté obnovit.
- [ ] Organizátor vidí tým jako „Inactive" v přehledu.
- [ ] Po reconnectu týmu se přítomnost vrátí na „Connected".
- [ ] Pokud session mezitím skončila, klient přejde na výsledky (nebo hlavní stránku při zrušení).

## Scénář 4 — Reload tabu (tým)
- [ ] Tým se připojí a je na otázkové obrazovce.
- [ ] Stiskni F5 (reload).
- [ ] Bootstrap stránka (`/tym/obnovit/{sessionId}`) načte snapshot a naviguje na správnou obrazovku.
- [ ] Odpovědi odeslané před reloadem jsou zachovány.

## Scénář 5 — Více týmů v jednom browseru
- [ ] Otevři dvě okna/taby, z každého se připoj jako jiný tým.
- [ ] Oba týmy vidí nezávislé odpovědi a stavy.
- [ ] Lokální lock odpovědi se nepropisuje mezi týmy (`sessionId + teamId + questionId`).
- [ ] Výsledky zobrazují správné zvýraznění pro každý tým.

## Scénář 6 — Výpadek sítě (organizátor)
- [ ] Organizátor je v čekárně se spuštěnou session.
- [ ] V DevTools přepni do offline režimu na 15 s, poté obnovit.
- [ ] Organizátor vidí „Reconnecting…" a po obnovení aktualizovaný stav.
- [ ] Start/Cancel tlačítka jsou během in-flight requestu disabled (ochrana proti double-click).

## Scénář 7 — Reload tabu (organizátor)
- [ ] Organizátor má otevřenou stránku se spuštěnou session.
- [ ] Stiskni F5.
- [ ] Po reloadu se na stránce detailu kvízu zobrazí banner „Obnovit řízení session".
- [ ] Kliknutím se organizátor vrátí do čekárny s aktuálním stavem.

## Scénář 8 — Session zrušena během offline (tým)
- [ ] Tým je offline, organizátor zatím session zruší.
- [ ] Po obnovení sítě klient zobrazí hlášku o zrušení a přesměruje na hlavní stránku.

## Scénář 9 — Submit odpovědi s výpadkem sítě (retry)
- [ ] Tým odešle odpověď, ale síť vypadne před přijetím server response.
- [ ] Po obnovení klient automaticky retryuje pending submit s `ClientRequestId`.
- [ ] Server idempotentně potvrdí submit (bez duplicitního zápisu).
- [ ] Klient potvrdí finální stav přes snapshot.

## Scénář 10 — Tab sleep / background (mobilní prohlížeč)
- [ ] Na mobilním zařízení přepni na jinou aplikaci na 15 s, poté se vrať.
- [ ] Klient se automaticky resynchronizuje.
- [ ] Odpočet je přepočítán dle serverového času.

---

## Stav ověření
Datum posledního ověření: —
Ověřeno na: —
Poznámky: —
