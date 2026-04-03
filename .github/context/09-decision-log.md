# Decision log

Zapisuj sem všechna doplněná rozhodnutí, která nebyla explicitně daná ve zdroji pravdy, ale byla zvolena proto, aby mohl vývoj plynule pokračovat.

## Pravidla zápisu
Každý záznam má mít:
- datum/čas v UTC, pokud je k dispozici,
- krátký identifikátor kroku,
- rozhodnutí,
- důvod,
- dopad.

---

## Počáteční doplněná rozhodnutí této workflow sady

### D-001 — Jemnější roadmapa než v původní analýze
- **Rozhodnutí:** Původních 9 fází bylo rozpadnuto do kroků S00–S21.
- **Důvod:** Umožňuje stabilní AI workflow po malých krocích s jednoduchým příkazem „Pokračuj k dalšímu kroku“.
- **Dopad:** Menší diffy, jednodušší review, nižší riziko driftu.

### D-002 — Přidán projekt `QuizApp.Tests`
- **Rozhodnutí:** Testy budou odděleny do samostatného projektu `QuizApp.Tests`.
- **Důvod:** Původní analýza explicitně nepojmenovala test projekt, ale automatické testy jsou potřeba pro bezpečný AI vývoj.
- **Dopad:** Stabilnější verifikace business pravidel.

### D-003 — Stavové soubory uvnitř `.github/context`
- **Rozhodnutí:** Stav implementace, decision log a changelog jsou uloženy v `.github/context`.
- **Důvod:** AI je má mít v jednom kontextovém prostoru s instrukcemi a roadmapou.
- **Dopad:** Lepší kontinuita mezi kroky.

### D-004 — Prompt files jsou pomocné, ne kritické
- **Rozhodnutí:** Workflow je navrženo tak, aby fungovalo i bez prompt files.
- **Důvod:** Prompt files jsou preview funkce.
- **Dopad:** Hlavní logika je v repo-wide instrukcích a stavových souborech.

---

## Další záznamy
Sem přidávej další rozhodnutí průběžně.

### D-200 — Organizátorský dashboard je globální napříč uživateli
- **Datum/čas (UTC):** 2026-04-02T00:00:00Z
- **Krok:** Post-S21 feature
- **Rozhodnutí:** Seznam v `OrganizerDashboard` se nově načítá serverově přes veřejný endpoint `GET /api/quizzes` a zobrazuje všechny existující kvízy bez vazby na lokální `browser storage`.
- **Důvod:** Uživatel explicitně požadoval, aby každý, kdo aplikaci otevře, viděl i kvízy vytvořené jinými uživateli.
- **Dopad:** Lokální úložiště už neslouží jako zdroj pravdy pro viditelnost seznamu; používá se jen pro uchování lokálního organizer tokenu k vlastním kvízům.

### D-005 — S00 přijato jako splněné na existující solution
- **Datum/čas (UTC):** 2026-03-25T11:08:22Z
- **Krok:** S00
- **Rozhodnutí:** Krok `S00` byl dokončen verifikací již existující solution a projektové kostry místo znovuvytváření projektů.
- **Důvod:** Uživatel explicitně uvedl, že prázdná solution je již založená.
- **Dopad:** Změny v tomto kroku jsou minimální a reverzibilní; pokračuje se na `S01`.

### D-006 — S01 bez DB health check závislého na provider balíčku
- **Datum/čas (UTC):** 2026-03-25T11:55:41Z
- **Krok:** S01
- **Rozhodnutí:** V kroku `S01` je použit obecný `health` endpoint přes `AddHealthChecks()` bez napojení na PostgreSQL-specific health check provider.
- **Důvod:** Cíl kroku je připravit hostingovou kostru, konfiguraci PostgreSQL a middleware bez předčasného rozšiřování závislostí.
- **Dopad:** Server je připravený na následné databázové kroky a endpoint `GET /health` vrací základní stav hostu.

### D-007 — S03 kontrakty navrženy jako transportní rekordy a enumy
- **Datum/čas (UTC):** 2026-03-25T12:54:41.9162834Z
- **Krok:** S03
- **Rozhodnutí:** Sdílené kontrakty v `QuizApp.Shared` jsou zavedeny jako jednoduché `record` DTO a enumy ve dvou oblastech (`Contracts`, `Enums`), včetně mapování `RealtimeEventName -> wire name`.
- **Důvod:** Krok S03 vyžaduje čitelné a minimální kontrakty bez předčasného uzamykání interní business logiky nebo persistence detailů.
- **Dopad:** Následující backendové kroky mají stabilní základ pro API a SignalR názvosloví bez zavádění nadbytečných závislostí.

### D-008 — S04 doménový model umístěn do serveru s explicitními guardy
- **Datum/čas (UTC):** 2026-03-25T13:40:00Z
- **Krok:** S04
- **Rozhodnutí:** Entitní model MVP je vytvořen v `QuizApp.Server/Domain/Entities` s privátními konstruktory, továrními metodami a jednoduchými guardy pro povinné hodnoty, UTC časy a základní rozsahy.
- **Důvod:** Krok S04 požaduje úplný doménový model a základní invariants bez předčasného zavádění persistence konfigurací.
- **Dopad:** Krok S05 může navázat čistým EF Core mapováním nad již stabilními entitami a navigacemi.

### D-009 — S05 unikátnost join code řešena globálním unique indexem
- **Datum/čas (UTC):** 2026-03-25T13:17:57.7467671Z
- **Krok:** S05
- **Rozhodnutí:** `QuizSession.JoinCode` je v EF mapování nastaven jako globálně unikátní (`HasIndex(...).IsUnique()`) místo partial indexu jen pro aktivní session.
- **Důvod:** Zadání vyžaduje provider-agnostické a srozumitelné řešení; partial index dle stavu session by zavedl provider-specific SQL a zbytečnou komplexitu v této fázi.
- **Dopad:** Business požadavek na praktickou unikátnost join code je pokryt jednoduchým přenositelným omezením, které neblokuje další kroky.

### D-010 — S06 bootstrap databáze přes automatické aplikování migrací při startu
- **Datum/čas (UTC):** 2026-03-25T14:10:49.5545393Z
- **Krok:** S06
- **Rozhodnutí:** Databázový bootstrap je řešen voláním `Database.MigrateAsync()` při startu serveru a migrace se spravují přes lokální `dotnet-ef` tool manifest.
- **Důvod:** Krok S06 vyžaduje první migraci a inicializační flow; tento přístup je jednoduchý, čitelný a drží se stávajícího EF Core stacku bez přidání nové aplikační vrstvy.
- **Dopad:** Po dostupnosti PostgreSQL se schema vytvoří/aplikuje konzistentně při startu i přes CLI (`dotnet dotnet-ef ...`), bez ruční SQL správy.

### D-011 — S07 striktní CSV kontrakt a validační parser v serverové aplikační vrstvě
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S07
- **Rozhodnutí:** CSV importní kontrakt je fixován na přesnou hlavičku `question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec` (`CsvQuizContract`) a parser `QuizCsvParser` vrací strukturovaný validační report přes `CsvValidationIssueDto` s `row + column + reason`.
- **Důvod:** Roadmapa pro S07 vyžaduje nepovolit volnější hlavičku a dodat parser/validátor odděleně od UI, včetně přesných chyb a ignorování prázdných řádků.
- **Dopad:** Navazující krok S08 může přímo použít připravený parse výstup (`CsvQuizImportParseResult`) pro vytvoření kvízu/import otázek bez změny validačních pravidel.

### D-012 — S08 import je jednorázový pouze pro prázdný kvíz
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S08
- **Rozhodnutí:** Služba `ImportQuizCsvAsync` odmítá import, pokud už kvíz obsahuje otázky, a vrací aplikační chybu místo přepsání dat.
- **Důvod:** Zdroj pravdy explicitně říká, že nový kvíz je prázdný a CSV se nahrává jednorázově.
- **Dopad:** Následné API/UI kroky mohou stavět na deterministickém `empty quiz -> single import` flow bez potřeby řešit merge nebo reimport.

### D-013 — S09 organizátorská autentizace rozšířena o Administrátorké heslo kvízu
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S09
- **Rozhodnutí:** Pro endpointy správy kvízu (`import`, `detail`, `delete`) je autentizace platná při znalosti `X-Organizer-Token` **nebo** `X-Quiz-Password`; operace smazání navíc vyžaduje správné Administrátorké heslo kvízu.
- **Důvod:** Repo instrukce obsahují uživatelskou preferenci průběžně měnit pravidlo tak, aby znalost hesla opravňovala k organizátorským úkonům.
- **Dopad:** Organizátorské API je použitelné i bez uloženého tokenu, přitom zůstává zachovaná ochrana mazání kvízu heslem a kontrola aktivních session.

### D-014 — S10 lokální dashboard staví na párech `quizId + organizer token`
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S10
- **Rozhodnutí:** Organizátorské UI ukládá a čte seznam kvízů výhradně z `localStorage` pod jedním klíčem jako seznam dvojic `quizId + QuizOrganizerToken`; detail/import zároveň umožňují ruční autentizaci přes heslo.
- **Důvod:** Produktová specifikace požaduje klientský seznam bez serverového list endpointu a repo preference vyžaduje průběžně podporovat heslo jako alternativní organizátorskou autentizaci.
- **Dopad:** Refresh prohlížeče zachová lokální seznam kvízů pro daný browser a UI zůstává použitelné i při chybějícím tokenu.

### D-015 — S10 hotfix: WASM klient používá explicitní API base URL + server povoluje localhost CORS
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S10
- **Rozhodnutí:** Klientský `HttpClient` čte `ApiBaseUrl` z `QuizApp.Client/wwwroot/appsettings.json` a server povoluje CORS pro localhost originy.
- **Důvod:** Při běhu klienta (`https://localhost:7184`) a serveru (`https://localhost:7174`) na různých portech neprocházelo vytvoření kvízu z UI, i když Swagger na serveru fungoval.
- **Dopad:** Organizátorské UI může volat API i v odděleném debug běhu a není závislé na stejném originu klienta a serveru.

### D-016 — S11 join code formát a autorizace vytvoření session
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S11
- **Rozhodnutí:** Vytvoření session (`POST /api/quizzes/{quizId}/sessions`) je autorizované stejně jako ostatní organizátorské operace (`X-Organizer-Token` **nebo** `X-Quiz-Password`) a join code je generován jako 8 znaků z abecedy `A-Z` bez ambiguit + `2-9`.
- **Důvod:** Uživatelská preference vyžaduje průběžně rozšiřovat pravidlo „znalost hesla opravňuje k organizátorským úkonům“ i na nové endpointy; zároveň roadmapa S11 požaduje neuhodnutelný join code bez zbytečné komplexity.
- **Dopad:** Session create backend je konzistentní s předchozí autentizací v S09/S10 a poskytuje prakticky bezpečný, čitelný join code s kontrolou unikátnosti.

### D-017 — S12 state snapshot vyžaduje reconnect token a join respektuje max. 20 týmů
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S12
- **Rozhodnutí:** Endpoint `GET /api/sessions/{sessionId}/state` vyžaduje hlavičku `X-Team-Reconnect-Token`; join endpoint navíc vynucuje unikátní název týmu (case-insensitive) a limit 20 týmů na session.
- **Důvod:** `00-source-of-truth.md` požaduje, aby týmové requesty po joinu neprocházely bez reconnect tokenu, a zároveň definuje max. 20 týmů v jedné session.
- **Dopad:** Reconnect identita je od S12 skutečně vynutitelná na backendu a join flow respektuje produktový limit bez rozšiřování scope mimo MVP.

### D-018 — S13 waiting room používá organizátorský snapshot endpoint
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S13
- **Rozhodnutí:** Pro organizátorskou čekárnu byl doplněn endpoint `GET /api/sessions/{sessionId}` vracející snapshot session (join code, stav, týmy) a autentizace je platná přes `X-Organizer-Token` **nebo** `X-Quiz-Password`.
- **Důvod:** S13 vyžaduje waiting room se seznamem připojených týmů; týmový snapshot endpoint ze S12 vyžaduje `X-Team-Reconnect-Token`, takže pro organizátora nebyl použitelný.
- **Dopad:** UI čekárny může bezpečně načítat aktuální stav WAITING session bez zavádění SignalR v tomto kroku a bez scope creep do game engine.

### D-019 — S14 cancel endpoint vyžaduje explicitní potvrzení v requestu
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S14
- **Rozhodnutí:** Endpoint `POST /api/sessions/{sessionId}/cancel` přijímá payload `CancelSessionRequest` a backend vyžaduje `ConfirmCancellation=true`; bez něj vrací validační chybu.
- **Důvod:** Roadmapa S14 požaduje potvrzovací krok pro zrušení session v UI; explicitní potvrzení v API kontraktu zajišťuje, že tento požadavek nepůjde obejít omylem klientskou chybou.
- **Dopad:** Cancel flow je bezpečnější a konzistentní mezi klienty, přitom scope zůstává v rámci MVP (jen start/cancel přechody a potvrzení zrušení).

### D-020 — S15 timeout progression je řešena serverovým background workerem
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S15
- **Rozhodnutí:** Otázkový engine běží plně na serveru: při `StartSession` se okamžitě nastaví první otázka a periodický hostovaný worker (`SessionProgressionBackgroundService`) posouvá běžící session po vypršení `QuestionDeadlineUtc`; po poslední otázce session přepne do `FINISHED`.
- **Důvod:** Roadmapa S15 vyžaduje deterministický server-authoritative průběh a automatický přechod po timeoutu i bez zásahu klienta.
- **Dopad:** Klienti dostávají konzistentní snapshoty bez serverového sekundového tick streamu a timeout progression funguje i bez aktivního pollingu konkrétního klienta.

### D-021 — S16 realtime eventy nesou minimální payload, klient refreshuje snapshot přes REST
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S16
- **Rozhodnutí:** SignalR vrstva publikuje do session-specific groups pouze názvy eventů (`team.joined`, `session.started`, `question.changed`, `session.cancelled`, `session.finished`, `results.ready`) bez přenosu plného stavu; klient po eventu načítá autoritativní snapshot přes existující REST endpoint.
- **Důvod:** Roadmapa S16 vyžaduje realtime synchronizaci bez sekundových ticků; minimální event payload drží protokol jednoduchý a zároveň zachovává server jako jediný zdroj pravdy pro stav session.
- **Dopad:** Waiting room se synchronizuje téměř okamžitě, reconnect flow je jednoduchý (SignalR resubscribe + REST refresh) a není nutné duplikovat snapshot kontrakty mezi REST a realtime vrstvou.

### D-022 — S17 dočasně uzamyká odpověď lokálně, backend submit přijde v S18
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S17
- **Rozhodnutí:** V týmovém question UI je „odeslání odpovědi“ v kroku S17 řešeno lokálním uložením a uzamčením per `sessionId + questionId` v `localStorage`; volání backend submit endpointu nebude přidáno dříve než v S18.
- **Důvod:** Roadmapa explicitně odděluje S17 (Team UI flow) od S18 (`POST /api/sessions/{sessionId}/answers` a `first-write-wins`), takže plná serverová submit logika by v S17 byla scope creep.
- **Dopad:** Týmové UI už nyní splňuje UX požadavek „po odeslání jasně uzamknout odpověď“, přičemž serverová autorita a business validace zůstanou doplněny v navazujícím kroku S18.

### D-023 — S18 submit request nese `QuestionId`, response nevrací `IsCorrect`
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S18
- **Rozhodnutí:** Kontrakt `POST /api/sessions/{sessionId}/answers` používá `SubmitAnswerRequest(TeamId, QuestionId, SelectedOption)` a úspěšná odpověď vrací pouze potvrzení uložení bez pole `IsCorrect`.
- **Důvod:** `QuestionId` umožňuje serveru bezpečně validovat, že submit patří právě aktivní otázce (ochrana proti race při přepnutí otázky), a nevracení `IsCorrect` drží pravidlo MVP „během hry nezobrazovat správné odpovědi“. 
- **Dopad:** Backend first-write-wins je deterministický i při pozdních/opožděných requestech a API neprozrazuje správnost odpovědi před krokem výsledků (`S19`).

### D-024 — S19 dual-auth výsledky, výpočet rankingu v ProgressDueSessionsAsync a oprava TeamQuestion submitu
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S19
- **Rozhodnutí:** (1) Endpoint `GET /api/sessions/{sessionId}/results` přijímá buď `X-Team-Reconnect-Token` (tým), nebo `X-Organizer-Token`/`X-Quiz-Password` (organizátor); (2) výpočet výsledků a `SessionResult` entit probíhá automaticky při finalizaci session v `ProgressDueSessionsAsync`; (3) `TeamQuestion.razor` byl opraven tak, aby skutečně volal backend submit endpoint z S18, místo pouze lokálního uzamčení ze S17.
- **Důvod:** (1) Týmy i organizátor potřebují vidět výsledky — sdílený endpoint s dual-auth je nejjednodušší MVP řešení. (2) Výpočet v progression workeru zaručuje, že výsledky existují okamžitě po přechodu do `FINISHED` bez nutnosti lazy computation. (3) S17 záměrně odložil backend submit na S18 (D-022), ale klient se v S18 nenapojil — oprava v S19 zajistí end-to-end funkčnost.
- **Dopad:** Výsledky jsou dostupné okamžitě po ukončení session, ranking je deterministický (skóre DESC, čas ASC, sdílený rank) a odpovědi se skutečně ukládají na server.

### D-025 — Explicitní StateHasChanged po SignalR async InvokeAsync
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Bugfix (mezi S19 a S20)
- **Rozhodnutí:** Ve všech Blazor WASM stránkách, které reagují na SignalR eventy přes `InvokeAsync(Func<Task>)`, je na konci callbacku voláno explicitní `StateHasChanged()`.
- **Důvod:** V Blazor WASM `ComponentBase.InvokeAsync(Func<Task>)` negarantuje automatický re-render po dokončení async práce, protože WASM dispatcher pouze deleguje volání na stejné vlákno bez povědomí o renderu.
- **Dopad:** UI správně reaguje na SignalR eventy (nová otázka, konec session apod.) bez nutnosti manuálního refreshe stránky.

### D-026 — Countdown timer přes System.Threading.Timer
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Bugfix (mezi S19 a S20)
- **Rozhodnutí:** Zbývající čas na otázku se zobrazuje jako živý countdown (sekundy), implementovaný přes `System.Threading.Timer` s 1s intervalem.
- **Důvod:** Surový UTC deadline byl pro hráče nečitelný a nebylo zřejmé, kolik zbývá času.
- **Dopad:** Hráč vidí odpočet v reálném čase; timer se automaticky restartuje při přechodu na novou otázku a uvolňuje v `DisposeAsync`.

### D-027 — S20 hardening: limiter policy + text sanitizace + reconnect okno 60s
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** S20
- **Rozhodnutí:** Hardening minimum je implementováno kombinací server-side rate limiting politik (`JoinPerIp`, `SubmitPerTeam`, `OrganizerMutations`), centralizované sanitizace textových vstupů (`TextInputSanitizer`) s délkovými limity dle persistence, TLS-ready middleware (`UseForwardedHeaders`, `UseHsts`) a klientské SignalR reconnect policy omezené na 60 sekund (`ReconnectWithinSixtySecondsPolicy`).
- **Důvod:** Roadmapa S20 explicitně požaduje rate limiting, sanitizaci vstupů, HTTPS/TLS připravenost a ošetřený reconnect do 60s bez rozšiřování scope mimo MVP.
- **Dopad:** Aplikace má základní provozně-bezpečnostní odolnost v API vrstvě i klientském realtime flow, při zachování jednoduché architektury modulárního monolitu.

### D-028 — S21 integrační testy přes WebApplicationFactory + InMemory a skip migrace v Testing
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** S21
- **Rozhodnutí:** API integrační testy běží přes `WebApplicationFactory<Program>` s přepnutím hostu do prostředí `Testing`, náhradou `QuizAppDbContext` za InMemory provider a vypnutím startup migrace (`MigrateAsync`) mimo test prostředí.
- **Důvod:** S21 vyžaduje integrační testy s vysokou hodnotou; v CI/lokálním test běhu bez dostupného PostgreSQL je potřeba deterministický a rychlý host bez externí DB závislosti.
- **Dopad:** Reálné HTTP flow testy ověřují mapování endpointů, hlavičkovou autentizaci a kontrakty bez ztráty rychlosti testů; produkční běh stále migruje databázi standardně.

### D-029 — Post-S21: databázový dluh je uzavřen po úspěšném `database update`
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Post-S21
- **Rozhodnutí:** Po potvrzeném běhu `dotnet dotnet-ef database update` v prostředí `Development` je původní riziko „nedostupný localhost:5432“ odstraněno ze sekce `Rizika / dluh`.
- **Důvod:** Otevřený dluh měl být evidován jen do okamžiku praktického ověření migrace proti lokálnímu PostgreSQL.
- **Dopad:** Stavové soubory nyní přesně odráží aktuální stav projektu; z provozních omezení zůstává jen ruční smoke-check browser/SignalR flow.

### D-030 — Bugfix: lokální uzamčení odpovědi je per tým a až po potvrzení serveru
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Post-S21 bugfix
- **Rozhodnutí:** `TeamSessionLocalStore` ukládá odpovědi pod klíčem `sessionId + teamId + questionId` a `TeamQuestion` ukládá lokální lock až po úspěšném submitu (resp. `AlreadyAnswered`), ne před HTTP voláním.
- **Důvod:** Původní lokální lock `sessionId + questionId` a předčasné ukládání mohly zablokovat další pokusy i po neúspěšném submitu a vést k nulovým výsledkům u týmů bez serverově potvrzené odpovědi.
- **Dopad:** UI lock nyní lépe odpovídá serverové realitě; tým se nezamkne při validační/network chybě a odpovědi se nepropisují mezi týmy v téže session na jednom klientovi.

### D-031 — Bugfix: týmová identita v klientovi je jednoznačná přes `teamId` v URL
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Post-S21 bugfix
- **Rozhodnutí:** Team flow (`čekárna -> otázka -> výsledky`) přenáší `teamId` v query parametru a `TeamSessionLocalStore` umožňuje více identit pro stejnou session; lookup identity preferuje přesný pár `sessionId + teamId`.
- **Důvod:** Při testování více týmů ve stejném browseru se původní model „jedna identita na session“ přepisoval a odpovědi se připisovaly špatnému týmu.
- **Dopad:** I při více týmech v jedné session a jednom browseru zůstává identita stabilní a submity se zapisují správnému týmu.

### D-032 — Organizátorský dashboard skrývá interní identifikátory a zobrazuje jen business metadata
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Post-S21 UI úprava
- **Rozhodnutí:** Grid na `OrganizerDashboard` zobrazuje pouze sloupce `Název kvízu` a `Datum založení`; `QuizId` a `OrganizerToken` zůstávají interně uložené, ale nejsou přímo viditelné v tabulce.
- **Důvod:** Požadavek UX je zobrazovat organizátorovi přehled business dat bez exponování technických identifikátorů/tokenu.
- **Dopad:** Lepší čitelnost dashboardu a menší riziko nechtěného sdílení citlivých údajů; pro existující záznamy se metadata dočítají přes detail endpoint s uloženým tokenem.

### D-033 — Tlačítko `Detail` zachováno bez rozšíření datových sloupců
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Post-S21 UI úprava
- **Rozhodnutí:** Akce `Detail` je vrácena jako tlačítko přímo v buňce `Název kvízu`, aby tabulka stále obsahovala jen dva datové sloupce (`Název kvízu`, `Datum založení`).
- **Důvod:** Uživatelsky je tlačítko výraznější než textový odkaz, ale současně má zůstat zachovaný požadavek na minimální datový grid bez `QuizId` a tokenu.
- **Dopad:** Lepší použitelnost seznamu bez porušení předchozí UX podmínky na obsah gridu.

### D-034 — `Detail` tlačítko je zarovnané na pravý okraj řádku
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Post-S21 UI úprava
- **Rozhodnutí:** Tlačítko `Detail` je vykresleno v pravé části druhého sloupce (`Datum založení`) s flex layoutem `justify-content-between`, aby bylo vizuálně úplně napravo.
- **Důvod:** Uživatel explicitně požádal o umístění akčního tlačítka na pravý okraj řádku.
- **Dopad:** Akce je konzistentně na stejném místě a tabulka stále zůstává dvousloupcová bez technických identifikátorů.

### D-035 — Join kód se zadává při startu session a čekárna se načítá automaticky
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Post-S21 UI/API úprava
- **Rozhodnutí:** Vytvoření session (`POST /api/quizzes/{quizId}/sessions`) nově vyžaduje request body s explicitním `JoinCode`; organizátorský detail kvízu proto obsahuje textbox join kódu a čekárna po přesměrování se `SessionId` automaticky načte bez ručního kliknutí na `Načíst snapshot`.
- **Důvod:** Uživatel požadoval zadání join kódu přímo při spuštění kvízu a odstranění mezikroku v čekárně.
- **Dopad:** Organizátor má plnou kontrolu nad kódem sdíleným hráčům a flow `Spustit kvíz -> čekárna` je jedním krokem bez zbytečné interakce navíc.

### D-036 — Join kód má mít jen minimální délku, bez dalších formátových omezení
- **Datum/čas (UTC):** 2026-03-27T00:00:00Z
- **Krok:** Post-S21 bugfix
- **Rozhodnutí:** Validace `CreateSessionRequest.JoinCode` byla uvolněna na jediné pravidlo „alespoň 4 znaky“; byly odstraněny požadavky na pevnou délku 8 znaků a omezenou znakovou sadu.
- **Důvod:** Uživatel explicitně požadoval, aby kód `ABCD1234` procházel a aby jediným omezením byla minimální délka.
- **Dopad:** Organizátor může použít libovolný join kód délky 4+ znaků, pokud je unikátní; UI i backend hlášky jsou konzistentní s tímto pravidlem.

### D-037 — Zavedení druhého typu otázky `NumericClosest` při zachování kompatibility
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 feature
- **Rozhodnutí:** Otázky mají nový typ `NumericClosest`; CSV import podporuje rozšířenou hlavičku s `question_type` a `correct_numeric_value`, přičemž původní 7-sloupcová hlavička zůstává validní jako `MultipleChoice`.
- **Důvod:** Uživatel požadoval druhý typ otázky s číselným tipem a bodováním nejbližších odpovědí bez rozbití existujícího obsahu a workflow.
- **Dopad:** Aplikace zvládá kombinaci multiple-choice i numerických otázek v jednom kvízu; scoring je deterministický (v numerické otázce bod dostanou všechny týmy se shodně nejmenší odchylkou).

### D-038 — Chybějící DB migrace pro `NumericClosest` se řeší jako explicitní Post-S21 bugfix
- **Datum/čas (UTC):** 2026-03-31T19:05:00Z
- **Krok:** Post-S21 bugfix
- **Rozhodnutí:** Byla vygenerována a aplikována migrace `20260331190153_AddNumericClosestQuestionFields` místo dočasných workaroundů (vypnutí background služby / ignorování výjimky).
- **Důvod:** Runtime pád (`42703 column q0.CorrectNumericValue does not exist`) byl způsoben nesouladem modelu a DB schématu; správné řešení v rámci MVP je dorovnat schéma přes EF migraci.
- **Dopad:** `Questions` a `TeamAnswers` mají očekávané sloupce pro numerické otázky, background progression služba již nepadá na chybějícím sloupci a klient přestává dostávat následné `TypeError: Failed to fetch` po pádu serveru.

### D-039 — Ruční vkládání otázek je samostatný endpoint místo opětovného CSV importu
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 feature
- **Rozhodnutí:** Pro ruční přidání otázek byl zaveden endpoint `POST /api/quizzes/{quizId}/questions` s novým kontraktem `AddQuizQuestionRequest`, namísto workaroundu přes generování jednorázového CSV importu.
- **Důvod:** Existující import je záměrně jednorázový pro prázdný kvíz; uživatelský požadavek na ruční vkládání otázek v detailu kvízu vyžaduje možnost přidávat otázky i po založení kvízu bez porušení import flow.
- **Dopad:** Organizátor může otázky přidávat přímo z UI (multiple-choice i numeric), backend zachovává stávající auth model (`token` nebo `heslo`) a úpravy jsou blokované při aktivní session (`WAITING`/`RUNNING`) kvůli determinismu hry.

### D-040 — Klientské desetinné hodnoty se zobrazují bez pevných koncových nul
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI bugfix
- **Rozhodnutí:** V klientských stránkách s numerickými otázkami a výsledky je použito jednotné formátování desetinných čísel bez nulového paddingu (`0.############################`) a u času v sekundách je při detailním zobrazení použito `0.###` místo pevného `F3`.
- **Důvod:** Uživatel požadoval, aby se desetinná místa zobrazovala jen tehdy, když obsahují relevantní číslice.
- **Dopad:** UI je čitelnější, bez hodnot typu `12.000`/`5.5000`; při zachování potřeby odlišit těsné časové rozdíly ve výsledcích.

### D-041 — `NumericClosest`: bod za nejbližší tip, `CorrectCount` jen za přesnou shodu
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 backend bugfix
- **Rozhodnutí:** Ve výpočtu session výsledků zůstává u `NumericClosest` pravidlo bodování nejbližších tipů pro `Score`, ale `CorrectCount` se navyšuje pouze při přesné numerické shodě (`NumericValue == CorrectNumericValue`).
- **Důvod:** Uživatel explicitně požadoval, aby sloupec `Správně` znamenal skutečně správné odpovědi, ne jen vítězný nejbližší odhad.
- **Dopad:** Výsledkový grid přesněji odlišuje „získané body“ od „fakticky správných odpovědí“ u numerických otázek.

### D-042 — Terminologie numerických výsledků: `Správná odpověď` místo `Správná hodnota`
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI text tweak
- **Rozhodnutí:** V klientských obrazovkách byl popisek u numerických správných hodnot sjednocen na `Správná odpověď:`.
- **Důvod:** Uživatel explicitně požadoval jednotnou terminologii.
- **Dopad:** Konzistentnější UX napříč detailem kvízu i výsledkovými stránkami.

### D-043 — `Otázky kvízu`: při `QuestionCount == 0` se ruční formulář zobrazí ihned
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** V `OrganizerQuizDetail` se formulář `Ruční vložení otázky` zobrazuje okamžitě, pokud kvíz nemá žádné otázky; flow se zadáním hesla a tlačítkem `Zobrazit otázky` zůstává pro kvízy, které už otázky obsahují.
- **Důvod:** Uživatel požadoval zrychlit první vložení otázky do nově vytvořeného (prázdného) kvízu bez mezikroku načítání.
- **Dopad:** První otázku lze přidat přímo po otevření detailu; po přidání první otázky se UI vrací do původního režimu zobrazení otázek.

### D-044 — Po prvním ručním vložení zachovat možnost přidávat další otázky bez hesla v aktuální relaci stránky
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** `OrganizerQuizDetail` drží dočasný UI režim, který po prvním úspěšném ručním vložení otázky umožní v tomtéž otevření stránky dál přidávat otázky bez zadávání `X-Quiz-Password`; tento režim se při novém otevření detailu resetuje.
- **Důvod:** Uživatel požadoval plynulé vkládání více otázek za sebou po založení prázdného kvízu, ale zároveň zachovat heslo při pozdějším návratu do detailu.
- **Dopad:** UX pro počáteční naplnění kvízu je rychlejší bez oslabení pravidla, že při dalším přístupu se otázky načítají přes heslo.

### D-045 — Ruční správa otázek používá explicitní pořadí bez automatického přeskládání
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 feature
- **Rozhodnutí:** Pro ruční `POST` i `PUT` otázky se používá explicitně zadané pořadí (`Order`), které musí být unikátní v rámci kvízu; při kolizi se operace odmítne validační chybou místo automatického přesunu ostatních otázek.
- **Důvod:** Uživatel explicitně požadoval validaci existence otázky s požadovaným pořadím a současně možnost řídit pořadí při vkládání/editaci.
- **Dopad:** Chování je deterministické a předvídatelné, bez skrytých side-effectů v pořadí ostatních otázek; UI může organizátorovi vrátit jasnou chybu při duplicitě pořadí.

### D-046 — Ve formuláři ručního vložení je `Pořadí otázky` první pole
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** Pole `Pořadí otázky` je v `OrganizerQuizDetail` přesunuto nad `Typ otázky` na první pozici formuláře ručního vložení/úpravy.
- **Důvod:** Uživatel explicitně požadoval prioritu zadání pořadí před ostatními vlastnostmi otázky.
- **Dopad:** UX lépe odpovídá workflow organizátora při plánování pořadí otázek.

### D-047 — Formulář `Ruční vložení otázky` je explicitně sbalitelný
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** Sekce ručního vložení/úpravy otázky v `OrganizerQuizDetail` byla upravena na rozbalovací formulář s malým toggle tlačítkem vpravo v řádku nadpisu.
- **Důvod:** Uživatel explicitně požadoval možnost celý formulář schovat a rozbalit kliknutím na malé tlačítko u textu `Ruční vložení otázky`.
- **Dopad:** Stránka detailu kvízu je přehlednější; organizátor může formulář držet sbalený a rozbalit ho jen při potřebě vkládání/editace.

### D-048 — Směr toggle šipky odpovídá stavu formuláře
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** Ikona v toggle tlačítku formuláře `Ruční vložení otázky` používá `▼` pro sbalený stav a `▲` pro rozbalený stav.
- **Důvod:** Uživatel explicitně požadoval intuitivní indikaci směru rozbalení/sbalení podle stavu formuláře.
- **Dopad:** Lepší čitelnost a konzistentní UX chování rozbalovací sekce.

### D-049 — Položky otázek v `Otázky kvízu` mají jemný kontrastní podklad
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** Jednotlivé položky seznamu otázek v `OrganizerQuizDetail` používají vlastní CSS třídu `quiz-question-item` s jemně odlišnou barvou pozadí oproti card background.
- **Důvod:** Uživatel explicitně požadoval lehké podbarvení jednotlivých otázek pro lepší vizuální oddělení.
- **Dopad:** Lepší přehlednost dlouhého seznamu otázek bez zásadní změny designu.

### D-050 — Podbarvení `quiz-question-item` je zesílené pro jasnou viditelnost
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** Styl `quiz-question-item` používá výraznější, ale stále jemný kontrast (`#eef4ff` + světle modrý border a `inset` linku).
- **Důvod:** Původní podbarvení bylo v praxi příliš subtilní a uživatel hlásil, že změna není viditelná.
- **Dopad:** Rozdíl mezi pozadím sekce a jednotlivými question kartami je okamžitě rozpoznatelný.

### D-051 — Styling otázek přesunut do `OrganizerQuizDetail.razor.css`
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** Styly pro `quiz-question-item` jsou definovány v komponentním souboru `QuizApp.Client/Pages/OrganizerQuizDetail.razor.css` místo globálního `app.css`.
- **Důvod:** Uživatel hlásil, že globální CSS úprava se neprojevuje; komponentní CSS isolation zajistí cílenou aplikaci stylu přímo na stránce detailu.
- **Dopad:** Podbarvení otázek je navázané na konkrétní komponentu a je spolehlivěji viditelné.

### D-052 — Finální fallback: podbarvení question card řešeno inline stylem
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** V `OrganizerQuizDetail.razor` je podbarvení question card a transparentní pozadí option řádků nastaveno přímo přes inline `style` atribut.
- **Důvod:** Uživatel opakovaně hlásil, že změna se neprojevuje; inline styl eliminuje vliv cache/bundlingu/priority selektorů.
- **Dopad:** Vizuální odlišení otázek je deterministické a okamžitě aplikované bez závislosti na externích CSS souborech.

### D-053 — Badge `Správná` používá zelený text místo bílého
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** Badge u správné odpovědi v `OrganizerQuizDetail` používá styl `bg-light text-success border border-success` namísto `text-bg-success`.
- **Důvod:** Uživatel požadoval, aby text `Správná` byl zelený stejně jako zvýraznění správné odpovědi v řádku.
- **Dopad:** Vizuální význam je konzistentní: správná odpověď i její badge používají stejnou zelenou barvu textu.

### D-054 — `Otázky kvízu` mají pevné pořadí metadat a `Upravit` v pravém horním rohu
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** V každé question card je nahoře řádek `Otázka číslo:` + akce `Upravit` vpravo; pod tím následuje text otázky, `Typ otázky:` a `Čas na odpověď:` v přesně daném pořadí.
- **Důvod:** Uživatel explicitně požadoval konkrétní strukturu informací v kartě otázky a umístění tlačítka `Upravit` do pravého horního rohu.
- **Dopad:** Konzistentní a rychle čitelný layout napříč všemi otázkami bez změny business logiky.

### D-055 — Text o heslu v `Otázky kvízu` se zobrazuje jen pokud kvíz má otázky
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** Informační text „Otázky i správné odpovědi se zobrazí až po zadání Administrátorského hesla kvízu.“ je podmíněně renderován jen když `QuestionCount > 0`.
- **Důvod:** Uživatel požadoval, aby se tento text nezobrazoval u prázdného kvízu, kde je primární flow ruční přidání první otázky.
- **Dopad:** Méně rušivé UI v prázdném stavu kvízu a konzistentnější první zkušenost s ručním vložením otázky.

### D-056 — `Import otázek CSV` je umístěn pod sekci `Otázky kvízu`
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** Na stránce `OrganizerQuizDetail` je sekce `Import otázek CSV` přesunuta za sekci `Otázky kvízu`.
- **Důvod:** Uživatel explicitně požadoval pořadí formulářů s prioritou ruční správy otázek nad CSV importem.
- **Dopad:** Struktura stránky lépe odpovídá požadovanému workflow organizátora.

### D-057 — Spuštění kvízu vyžaduje kompletní pořadí otázek bez mezer
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 backend validation
- **Rozhodnutí:** `CreateSessionAsync` před vytvořením session validuje, že `OrderIndex` otázek tvoří souvislou sekvenci `0..N-1`; pokud ne, vrací `ValidationFailed` s uživatelskou hláškou o nekompletním pořadí.
- **Důvod:** Uživatel explicitně požadoval zablokovat spuštění kvízu, když pořadí otázek není kompletní.
- **Dopad:** Organizátor nemůže spustit session nad nekonzistentním pořadím otázek; chyba je vrácena okamžitě při pokusu o spuštění.

### D-058 — Frontend validace ručního formuláře otázek je explicitně per pole
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI validation
- **Rozhodnutí:** `OrganizerQuizDetail` validuje formulář ručního vložení/úpravy otázky na klientu ještě před HTTP requestem; chyby se zobrazují krátce a konkrétně pod příslušným polem.
- **Důvod:** Uživatel explicitně požadoval „řádné frontendové validace“ se stručnými, ale vysvětlujícími chybovými informacemi.
- **Dopad:** Rychlejší oprava vstupu bez zbytečných round-tripů na backend a čitelnější UX při vyplňování formuláře.

### D-059 — `Pořadí otázky` se po uložení nastavuje na nejnižší volnou hodnotu
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** V `OrganizerQuizDetail` se pro předvyplnění `Pořadí otázky` používá helper, který hledá nejnižší volné pořadí; při načteném seznamu vychází ze skutečných `OrderIndex` otázek.
- **Důvod:** Uživatel explicitně požadoval po ručním přidání otázky automatický posun na „nejbližší nejnižší volnou hodnotu“.
- **Dopad:** Formulář po přidání lépe navádí na další validní pořadí i při mezerách v pořadí.

### D-060 — CSV import sjednocen na delimiter `;` + podpora Excel `sep=;`
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UX/API tweak
- **Rozhodnutí:** CSV parser používá jako oddělovač středník (`;`) a navíc akceptuje volitelný první řádek `sep=;` pro přímé otevření souboru v Excelu.
- **Důvod:** Uživatel explicitně požadoval, aby import i předpis fungoval v Excelu tak, že se hodnoty rozdělí do samostatných buněk.
- **Dopad:** Šablona i importní backend jsou konzistentní s českým Excel workflow; data lze otevřít/importovat bez ručního přepínání oddělovače.

### D-061 — Smazání otázky při editaci otázky v organizátorském UI
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI/API tweak
- **Rozhodnutí:** Do flow úpravy otázky je doplněna samostatná akce mazání (`DELETE /api/quizzes/{quizId}/questions/{questionId}`), která po smazání otázky automaticky přepočítá pořadí zbývajících otázek (`OrderIndex`) tak, aby zůstalo souvislé od nuly.
- **Důvod:** Uživatel explicitně požadoval možnost smazat aktuálně upravovanou otázku přímo v edit formuláři.
- **Dopad:** Organizátor může otázku odstranit bez ručního zásahu do databáze; kvíz po mazání nezůstane v nekonzistentním pořadí, takže lze dál bez překážek upravovat otázky i spouštět session.

### D-062 — Po CSV importu se otázky načtou automaticky do sekce `Otázky kvízu`
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UX tweak
- **Rozhodnutí:** Po úspěšném `ImportCsvAsync` klient automaticky volá `LoadQuestionsAsync` (pokud se skutečně importovala alespoň jedna otázka), aby se nové otázky zobrazily ihned bez manuálního kroku `Zobrazit otázky`.
- **Důvod:** Uživatel explicitně požadoval okamžité zobrazení nahraných otázek po CSV importu.
- **Dopad:** Kratší a plynulejší organizátorský workflow; po importu je stav kvízu okamžitě viditelný v otázkové sekci.

### D-063 — CSV upload v klientu má fallback dekódování pro české ANSI/Excel soubory
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 bugfix
- **Rozhodnutí:** `OrganizerQuizDetail` při načítání CSV souboru dekóduje obsah nejprve jako UTF-8 s `throwOnInvalidBytes=true`; pokud dekódování selže, použije fallback Windows-1250 (`CP1250`).
- **Důvod:** Uživatel nahlásil rozpad české diakritiky (`Kolik stup�� je prav� �hel?`) po importu CSV exportovaného mimo UTF-8 (typicky Excel/ANSI).
- **Dopad:** Import zůstává kompatibilní s UTF-8 i běžnými českými Windows exporty a texty otázek se na frontendu zobrazují korektně.

### D-064 — Po smazání otázky se edit formulář automaticky sbalí
- **Datum/čas (UTC):** 2026-03-31T00:00:00Z
- **Krok:** Post-S21 UI tweak
- **Rozhodnutí:** V `OrganizerQuizDetail` se po úspěšném `DeleteEditedQuestionAsync` nastaví `isManualQuestionFormExpanded = false`, takže se formulář `Ruční vložení otázky` po smazání otázky automaticky sbalí.
- **Důvod:** Uživatel explicitně požadoval automatické sbalení formuláře po akci `Smazat otázku`.
- **Dopad:** Po smazání je obrazovka přehlednější a organizátor se vrací k seznamu otázek bez ručního klikání na toggle formuláře.

### D-065 — R01: Sjednocený reconnect UX contract pro tým i organizátora
- **Datum/čas (UTC):** 2026-04-03T00:00:00Z
- **Krok:** R01
- **Rozhodnutí:** Klientský reconnect lifecycle je normativně sjednocen do stavů `Online`, `Reconnecting`, `Offline`, `Resynced`, `SessionEnded` pro tým i organizátora; UI stavy mají jednotné hlášky a explicitní akce bez dead-end větví.
- **Důvod:** Backlog krok R01 vyžaduje jednoznačnou specifikaci reconnect stavů a UX kontraktu dříve, než začne implementace heartbeat/versioning/fallback poll v R02+.
- **Dopad:** Následující kroky R02–R08 mají pevný referenční kontrakt pro chování klienta při výpadku/reconnectu; testy v R09 mohou validovat deterministický stavový automat místo ad-hoc UI chování.

#### R01 — Normativní stavový automat
- `Online`
  - Význam: SignalR připojeno a poslední snapshot je považován za aktuální.
  - UI hláška: „Připojeno k live session.“
  - Akce: běžné akce podle role (`Start/Cancel`, `Odeslat odpověď`, navigace dle stavu session).
- `Reconnecting`
  - Význam: dočasná ztráta realtime transportu, probíhá automatický reconnect v 60s okně.
  - UI hláška: „Obnovuji připojení…“
  - Akce: povoleno `Zkusit znovu` (okamžitý resync pokus), mutační akce jsou dočasně disabled.
- `Offline`
  - Význam: realtime reconnect se nepodařil nebo vypršel reconnect window; klient je bez potvrzeného živého stavu.
  - UI hláška: „Připojení se nepodařilo obnovit. Ověřte síť a zkuste to znovu.“
  - Akce: `Zkusit znovu`, `Obnovit snapshot` (REST), bezpečný návrat na čekárnu (pokud je route relevantní).
- `Resynced`
  - Význam: po reconnectu byl úspěšně načten aktuální server snapshot; klient mohl přeskočit mezistavy (`WAITING` -> `RUNNING`/`FINISHED`/`CANCELLED`).
  - UI hláška: „Připojení obnoveno. Stav byl synchronizován.“
  - Akce: jednorázové potvrzení a automatický přechod na správnou obrazovku dle snapshotu.
- `SessionEnded`
  - Význam: server autoritativně hlásí terminální stav session (`FINISHED` nebo `CANCELLED`).
  - UI hláška (tým): „Kvíz byl ukončen.“ + podmíněná informace o výsledcích.
  - UI hláška (organizátor): „Session je ukončena.“
  - Akce: tým přechází na výsledky (nebo čeká na zveřejnění výsledků), organizátor na výsledky/správné odpovědi; mutace session jsou disabled.

#### R01 — Role-specific UX pravidla
- **Tým**
  - V `Reconnecting`/`Offline` se nesmí tvářit, že odpověď byla definitivně přijata; potvrzení je validní až po resync/snapshotu.
  - V `SessionEnded` zůstává tlačítko `Zobrazit výsledky` viditelné; před zveřejněním je disabled a hláška je nad tlačítkem.
- **Organizátor**
  - V `Reconnecting`/`Offline` jsou `Start`/`Cancel` disabled, aby nevznikal double-submit během neověřeného stavu.
  - Po `Resynced` musí waiting room vždy respektovat server autoritu (stav session + seznam týmů) a nesmí zachovat stale lokální view.

#### R01 — Zakázané dead-end stavy
- Klient nesmí zůstat bez akce v režimu „chyba připojení“; vždy existuje minimálně `Zkusit znovu`.
- Přechod na neplatnou obrazovku po reconnectu (např. zůstat na otázce po `FINISHED`) je zakázaný; routing vždy přepisuje autoritativní snapshot.
- Lokální stav nikdy nepřebíjí serverový snapshot po reconnectu.

### D-066 — R02: Server-side presence přes heartbeat endpointy + minimální reconnect audit
- **Datum/čas (UTC):** 2026-04-03T00:00:00Z
- **Krok:** R02
- **Rozhodnutí:** Presence vrstva je server-authoritative přes heartbeat endpointy `POST /api/sessions/{sessionId}/heartbeat/team` a `POST /api/sessions/{sessionId}/heartbeat/organizer`; přítomnost se klasifikuje do `Connected` (<=15 s), `TemporarilyDisconnected` (<=90 s) a `Inactive` (>90 s), přičemž troubleshooting audit minimum zahrnuje `TEAM_DISCONNECTED`, `TEAM_RECONNECTED`, `ORGANIZER_HEARTBEAT`, `ORGANIZER_RECONNECTED` a `ORGANIZER_DISCONNECTED`.
- **Důvod:** Backlog R02 vyžaduje periodický update presence, rozlišení krátkodobého výpadku vs dlouhé neaktivity bez zásahu do skórování a minimální audit reconnect/disconnect eventů.
- **Dopad:** Organizátor i tým mají v snapshotu konzistentní serverové vyhodnocení přítomnosti a provozní troubleshooting má explicitní auditní stopu reconnect/disconnect přechodů bez změny scoring pravidel.

### D-067 — R03: Snapshot versioning přes UTC ticks + klientský stale guard
- **Datum/čas (UTC):** 2026-04-03T00:00:00Z
- **Krok:** R03
- **Rozhodnutí:** Snapshot kontrakty `SessionStateSnapshotResponse` a `OrganizerSessionSnapshotResponse` nesou monotónní `Version` (odvozené jako `ServerUtcNow.UtcDateTime.Ticks`) a explicitní serverový čas `ServerUtcNow`; klientské stránky aplikují snapshot jen pokud není starší než aktuálně držená verze (`incoming.Version < current.Version` se ignoruje).
- **Důvod:** Backlog R03 požaduje deterministickou resynchronizaci po reconnectu a ochranu proti stale response, aby server zůstal autoritou a pozdní odpovědi nepřepisovaly UI na starší stav.
- **Dopad:** Tým i organizátor mají jednotný mechanismus „latest snapshot wins“, který omezuje race conditions mezi realtime refreshi a REST odpověďmi a připravuje základ pro idempotentní realtime handler v navazujícím kroku R04.

### D-062 — R04: subscribe ack + fallback polling + idempotence podle verze
- **Datum/čas (UTC):** 2026-04-03T00:00:00Z
- **Krok:** R04
- **Rozhodnutí:** SignalR subscribe metoda `SubscribeToSessionAsync` vrací explicitní potvrzení (`bool`) a klient považuje realtime za obnovené až po úspěšném ack; při výpadku/reconnectu klientské stránky přechází do fallback `REST` poll režimu s intervalem 3 s a po obnovení hubu se vrací zpět na realtime.
- **Důvod:** Backlog R04 vyžaduje deterministické potvrzení resubscribe, odolnost při výpadku realtime vrstvy a prevenci duplicitních UI aktualizací.
- **Dopad:** Waiting room i question obrazovky zůstávají konzistentní i při dočasně nedostupném hubu; realtime eventy se zpracují idempotentně díky guardu `incoming.Version <= current.Version`.
