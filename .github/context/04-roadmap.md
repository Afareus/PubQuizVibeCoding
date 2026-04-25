# Roadmapa dalšího vývoje

Tato roadmapa začíná po dokončení původní Pub kvíz aplikace.

## Stav původní aplikace

- [x] Live Pub kvíz aplikace existuje.
- [x] Organizer/Player flow existuje.
- [x] Databáze a deployment základ existují.
- [x] Původní MVP dokumentace byla zredukována, aby agent zbytečně nereimplementoval hotové části.

## Nový cíl

Implementovat samostatný virální Challenge MVP:

```text
Kdo mě zná nejlíp?
```

## CH-01 — Datový model a EF Core migrace

### Cíl
Přidat databázový model pro Challenge mód bez zásahu do existující live session logiky.

### Udělej
- Zkontroluj existující solution a aktuální styl entit.
- Přidej entity:
  - `Challenge`
  - `ChallengeQuestion`
  - `ChallengeAnswerOption`
  - `ChallengeSubmission`
  - `ChallengeSubmissionAnswer`
- Přidej EF Core konfigurace podle existujícího stylu.
- Přidej indexy a unikátní omezení podle `02-architecture-and-data-model.md`.
- Přidej migraci.
- Před i po změně se pokus spustit build.

### Nedělej
- Neměň existující Pub kvíz entity, pokud to není nutné.
- Nepřidávej UI.
- Nepřidávej API endpointy.
- Nepřidávej SignalR.

### Hotovo, když
- Build projde.
- Migrace je vygenerovaná.
- Datový model odpovídá specifikaci.
- Stavové soubory jsou aktualizované.

### Ruční kontrola
- Zkontrolovat migraci, že nevytváří nečekané změny ve starých tabulkách.

---

## CH-02 — Shared DTO a validační kontrakty

### Cíl
Přidat sdílené request/response typy pro Challenge mód.

### Udělej
- Přidej DTO pro:
  - template,
  - vytvoření challenge,
  - detail challenge pro hraní,
  - odeslání odpovědí,
  - leaderboard,
  - výsledek submission.
- Dodrž, že DTO pro hraní nesmí obsahovat správné odpovědi.
- Přidej jednoduché validační konstanty, pokud to odpovídá stylu projektu.

### Nedělej
- Neimplementuj endpointy.
- Neimplementuj UI.

### Hotovo, když
- DTO jsou ve `QuizApp.Shared` nebo ekvivalentu.
- Build projde.
- Názvy jsou čitelné a konzistentní.

### Ruční kontrola
- Ověřit, že žádný response typ neposílá `CreatorSelectedOptionKey`.

---

## CH-03 — Challenge aplikační služba

### Cíl
Implementovat serverovou business logiku pro tvorbu, načtení, skórování a leaderboard.

### Udělej
- Přidej službu pro Challenge mód.
- Implementuj pevnou šablonu 10 otázek.
- Implementuj vytvoření challenge.
- Implementuj načtení challenge pro hráče.
- Implementuj odeslání odpovědí.
- Implementuj výpočet skóre.
- Implementuj leaderboard top 20.
- Přidej relevantní unit/integration testy, pokud projekt testy má.

### Nedělej
- Nepřidávej UI.
- Neměň live Pub kvíz services.
- Nezobrazuj správné odpovědi klientovi před submission.

### Hotovo, když
- Business pravidla z `01-product-spec.md` jsou pokrytá.
- Build a relevantní testy projdou.

### Ruční kontrola
- Ověřit testem nebo debugem, že submission se skóruje správně.

---

## CH-04 — HTTP API endpointy

### Cíl
Zpřístupnit Challenge službu přes REST API.

### Udělej
- Přidej endpointy:
  - `GET /api/challenges/template`
  - `POST /api/challenges`
  - `GET /api/challenges/{publicCode}`
  - `POST /api/challenges/{publicCode}/submissions`
  - `GET /api/challenges/{publicCode}/leaderboard`
- Použij existující styl controllerů nebo minimal APIs.
- Dodrž chybový model aplikace.
- Ověř, že endpoint pro detail challenge nevrací správné odpovědi.

### Nedělej
- Nepřidávej SignalR.
- Nepřidávej login.
- Nepřidávej administraci challenge.

### Hotovo, když
- Endpointy fungují.
- Build projde.
- Relevantní API testy nebo ruční HTTP testy dávají smysl.

### Ruční kontrola
- Vytvořit challenge přes API.
- Načíst challenge přes public code.
- Odeslat odpovědi.
- Získat leaderboard.

---

## CH-05 — UI pro vytvoření challenge

### Cíl
Přidat mobilně pohodlnou stránku `/challenge/create`.

### Udělej
- Přidej obrazovku pro zadání jména a názvu.
- Načti šablonu otázek.
- Umožni tvůrci vybrat odpověď pro každou otázku.
- Po vytvoření zobraz veřejný odkaz a sdílecí text.
- Přidej tlačítko pro zkopírování odkazu, pokud to jde jednoduše.

### Nedělej
- Nepřidávej vlastní otázky.
- Nepřidávej AI generování.
- Nepřidávej registraci.

### Hotovo, když
- Uživatel vytvoří challenge z UI.
- Vidí veřejný odkaz.
- Build projde.

### Ruční kontrola
- Na mobilním rozlišení vytvořit challenge za cca 1 minutu.

---

## CH-06 — UI pro hraní challenge

### Cíl
Přidat stránku `/challenge/{publicCode}` pro hráče.

### Udělej
- Načti challenge podle public code.
- Zobraz název, tvůrce a otázky.
- Hráč zadá jméno.
- Hráč vybere právě jednu odpověď u každé otázky.
- Odešli submission.
- Po úspěchu přesměruj nebo zobraz výsledek.

### Nedělej
- Nezobrazuj správné odpovědi před odesláním.
- Nepřidávej časomíru.
- Nepřidávej live prvky.

### Hotovo, když
- Hráč odehraje challenge z veřejného odkazu.
- Výsledek se uloží.
- Build projde.

### Ruční kontrola
- Otevřít odkaz v anonymním okně a odehrát jako hráč.

---

## CH-07 — Výsledek, leaderboard a virální CTA

### Cíl
Dokončit virální smyčku.

### Udělej
- Zobraz skóre hráče.
- Zobraz leaderboard top 20.
- Přidej sdílecí text po dohrání.
- Přidej výrazné CTA `Vytvořit vlastní kvíz`.
- CTA musí vést na `/challenge/create`.

### Nedělej
- Nepřidávej platební prvky.
- Nepřidávej reklamy.
- Nepřidávej veřejný katalog.

### Hotovo, když
- Hráč po dokončení vidí skóre, pořadí a CTA.
- Další uživatel může přes CTA vytvořit vlastní challenge.

### Ruční kontrola
- Odehrát challenge dvěma různými jmény a ověřit pořadí.

---

## CH-08 — Vstup do Challenge módu z aplikace

### Cíl
Udělat Challenge mód objevitelný, ale nerozbít původní hlavní flow.

### Udělej
- Přidej odkaz/dlaždici na Challenge mód na vhodné místo v aplikaci.
- Texty drž krátké:
  - `Kdo mě zná nejlíp?`
  - `Vytvořit zábavný kvíz pro přátele`
- Zachovej existující Organizer/Player navigaci.

### Nedělej
- Nepřepisuj homepage kompletně, pokud to není nutné.
- Neodstraňuj existující vstupy do Pub kvízu.

### Hotovo, když
- Uživatel najde Challenge mód z aplikace.
- Starý Pub kvíz flow je stále dostupný.

### Ruční kontrola
- Projít homepage / navigaci na desktopu i mobilu.

---

## CH-09 — Stabilizace, testy a release checklist

### Cíl
Připravit Challenge MVP k nasazení.

### Udělej
- Spusť build.
- Spusť testy, pokud existují.
- Projdi smoke testy z `07-manual-smoke-tests.md`.
- Oprav drobné chyby.
- Aktualizuj `11-release-checklist.md`.
- Zapiš konečný stav do `08-implementation-state.md`.

### Nedělej
- Nepřidávej novou velkou funkcionalitu.
- Nepřidávej AI, platby ani vlastní otázky.

### Hotovo, když
- Challenge MVP projde manuálním testem od vytvoření po virální CTA.
- Původní Pub kvíz aplikace stále funguje.
