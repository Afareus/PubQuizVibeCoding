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

### D-013 — S09 organizátorská autentizace rozšířena o mazací heslo
- **Datum/čas (UTC):** 2026-03-26T00:00:00Z
- **Krok:** S09
- **Rozhodnutí:** Pro endpointy správy kvízu (`import`, `detail`, `delete`) je autentizace platná při znalosti `X-Organizer-Token` **nebo** `X-Quiz-Password`; operace smazání navíc vyžaduje správné mazací heslo.
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
