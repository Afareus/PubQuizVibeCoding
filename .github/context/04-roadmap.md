# Roadmapa implementace po malých krocích

Tato roadmapa je rozpadnutá jemněji než původních 9 fází, aby bylo možné bezpečně fungovat stylem:
**zkontroluj -> potvrď -> „Pokračuj k dalšímu kroku“**.

## Pravidla pro práci s roadmapou
- Vždy implementuj pouze první nedokončený krok.
- Každý krok musí být samostatně buildovatelný.
- Po každém kroku aktualizuj stav, decision log a changelog.
- Pokud při kroku narazíš na chybu z předchozího kroku, nejdřív ji oprav a stále zůstaň ve stejném kroku.

---

## Krok S00 — Bootstrap repozitáře a solution
**Cíl**  
Založit základní solution a minimální projektovou kostru.

**Uděláš**
- vytvoříš solution,
- vytvoříš projekty `QuizApp.Client`, `QuizApp.Server`, `QuizApp.Shared`, `QuizApp.Tests`,
- nastavíš reference mezi projekty,
- ověříš, že solution buildí i s placeholder implementací.

**Nesmíš měnit**
- stack,
- názvy hlavních projektů,
- architekturu na jiný model než Client / Server / Shared.

**Hotovo když**
- solution existuje,
- projekty jsou součástí solution,
- build projde.

**Ruční kontrola**
- solution se otevře bez chyb,
- všechny 4 projekty jsou vidět v Solution Exploreru.

---

## Krok S01 — Základ hostingu a konfigurace serveru
**Cíl**  
Připravit serverovou kostru, konfiguraci a základní middleware pipeline.

**Uděláš**
- nastavíš ASP.NET Core host,
- připravíš konfiguraci pro PostgreSQL,
- přidáš health endpoint,
- připravíš základní registraci služeb a konfiguraci SignalR.

**Nesmíš měnit**
- nepřidávej business logiku,
- nepřidávej auth systém.

**Hotovo když**
- server startuje,
- health endpoint vrací úspěch,
- build projde.

**Ruční kontrola**
- server se spustí,
- health endpoint odpoví.

---

## Krok S02 — Základ klienta a routingu
**Cíl**  
Připravit navigaci a základní obrazovky bez finální logiky.

**Uděláš**
- landing page,
- role volbu organizátor / tým,
- základ routingu,
- placeholdery hlavních obrazovek.

**Nesmíš měnit**
- žádné finální API napojení kromě nutného minima,
- žádné přidávání funkcí mimo specifikaci.

**Hotovo když**
- klientská aplikace běží,
- všechny klíčové stránky mají dosažitelnou routu.

**Ruční kontrola**
- lze přepnout mezi hlavními obrazovkami,
- UI je v češtině.

---

## Krok S03 — Sdílené kontrakty a enumy
**Cíl**  
Vytvořit sdílené enumy, základ DTO a kontrakty používané napříč aplikací.

**Uděláš**
- enumy pro session status, option keys, error codes, event names,
- základ request/response DTO pro create quiz, import, session create, join, state snapshot.

**Nesmíš měnit**
- nesmíš do DTO propisovat hashovaná tajemství,
- nesmíš předčasně finálně uzamknout všechny kontrakty, které ještě nejsou potřeba.

**Hotovo když**
- sdílený projekt obsahuje čitelné kontrakty pro první backendové kroky,
- projekty buildí.

**Ruční kontrola**
- DTO a enumy odpovídají názvosloví ze specifikace.

---

## Krok S04 — Entitní model domény
**Cíl**  
Vytvořit entity a jejich základní vztahy.

**Uděláš**
- entity `Quiz`, `Question`, `QuestionOption`, `QuizSession`, `Team`, `TeamAnswer`, `SessionResult`, `AuditLog`,
- navigace a základní invariants,
- pomocné factory/metody jen tam, kde zvyšují čitelnost.

**Nesmíš měnit**
- žádné předčasné repository patterny navíc,
- žádné UI změny.

**Hotovo když**
- model odpovídá specifikaci,
- žádná klíčová entita nechybí.

**Ruční kontrola**
- model pokrývá všechny business pojmy ze specifikace.

---

## Krok S05 — EF Core mapování a DbContext
**Cíl**  
Zafixovat persistence vrstvu.

**Uděláš**
- `DbContext`,
- entity konfigurace,
- indexy a unique constraints,
- concurrency konfiguraci pro `QuizSession`.

**Nesmíš měnit**
- nepoužívej lazy loading,
- nepoužívej provider-specific hacky bez důvodu.

**Hotovo když**
- schéma odpovídá pravidlům,
- unique constraints podporují business pravidla.

**Ruční kontrola**
- zkontroluj mapování kritických omezení: týmové jméno, answer uniqueness, logické smazání.

---

## Krok S06 — První migrace a databázový bootstrap
**Cíl**  
Mít fyzicky vytvořitelné schéma databáze.

**Uděláš**
- vytvoříš první migraci,
- zapojíš inicializační flow nebo popis, jak databázi vytvořit,
- ověříš běh proti PostgreSQL.

**Nesmíš měnit**
- žádná business logika navíc.

**Hotovo když**
- migrace jde vytvořit a aplikovat,
- aplikace se umí připojit k DB.

**Ruční kontrola**
- databáze se založí,
- tabulky odpovídají modelu.

---

## Krok S07 — CSV kontrakt, parser a validační report
**Cíl**  
Připravit importní jádro odděleně od UI.

**Uděláš**
- parser CSV,
- validátor kontraktu,
- strukturovaný validační report s řádkem, sloupcem a důvodem,
- ignorování prázdných řádků.

**Nesmíš měnit**
- nepodporuj žádný jiný formát než CSV,
- nepovoluj volnější hlavičku.

**Hotovo když**
- validní CSV projde,
- nevalidní vrátí přesné chyby.

**Ruční kontrola**
- vyzkoušej validní a nevalidní CSV soubor.

---

## Krok S08 — Služba pro založení kvízu a import otázek
**Cíl**  
Zprovoznit jádro správy kvízů bez kompletního UI.

**Uděláš**
- create quiz service,
- jednorázové vrácení organizer tokenu,
- import service navázanou na create quiz / empty quiz flow,
- hashování mazacího hesla a organizer tokenu,
- audit log pro `QUIZ_CREATED` a `QUIZ_IMPORTED`.

**Nesmíš měnit**
- nepřidávej editaci otázek,
- neukládej token ani heslo čitelně.

**Hotovo když**
- lze programově vytvořit kvíz a naimportovat otázky,
- token se vrátí jen jednou při vytvoření.

**Ruční kontrola**
- vytvoření vrátí token,
- import vytvoří otázky v pořadí.

---

## Krok S09 — REST endpointy pro správu kvízů
**Cíl**  
Zveřejnit backendové kontrakty pro create/import/detail/delete.

**Uděláš**
- `POST /api/quizzes`
- `POST /api/quizzes/{quizId}/import-csv`
- `GET /api/quizzes/{quizId}`
- `DELETE /api/quizzes/{quizId}`
- správný error model a auth hlavičky.

**Nesmíš měnit**
- nesmí vzniknout globální list endpoint všech kvízů.

**Hotovo když**
- endpointy fungují,
- delete respektuje token + heslo + zákaz při aktivní session.

**Ruční kontrola**
- detail funguje jen s validním tokenem,
- delete odmítne špatné heslo.

---

## Krok S10 — Organizátorské UI pro kvízy
**Cíl**  
Dodat použitelné minimum UI pro create/import/detail/delete.

**Uděláš**
- dashboard z local storage,
- formulář Nový kvíz,
- uložení `quizId + QuizOrganizerToken` do local storage,
- upload CSV a zobrazení validačního reportu,
- detail kvízu,
- smazání s heslem.

**Nesmíš měnit**
- nepřidávej serverový seznam všech kvízů,
- nepřidávej editaci otázek.

**Hotovo když**
- organizátor může čistě z UI vytvořit kvíz a nahrát otázky.

**Ruční kontrola**
- refresh zachová lokální seznam kvízů v daném browseru.

---

## Krok S11 — Session create backend a join code
**Cíl**  
Připravit backend pro založení session ve stavu WAITING.

**Uděláš**
- create session service,
- generaci dostatečně neuhodnutelného join code,
- kontrolu, že kvíz má aspoň jednu otázku,
- audit log `SESSION_CREATED`.

**Nesmíš měnit**
- nepovoluj start session v tomto kroku,
- nepovoluj join po startu.

**Hotovo když**
- session lze vytvořit pouze nad validním kvízem s otázkami,
- session začíná ve stavu `WAITING`.

**Ruční kontrola**
- opakované create session nad stejným kvízem funguje.

---

## Krok S12 — Team join backend a reconnect identita
**Cíl**  
Zprovoznit připojení týmů a jejich identitu.

**Uděláš**
- `POST /api/sessions/join`,
- validaci join code,
- validaci unikátního názvu týmu v session,
- generování a jednorázové vrácení `TeamReconnectToken`,
- `GET /api/sessions/{sessionId}/state?teamId={teamId}`.

**Nesmíš měnit**
- nepovoluj join mimo `WAITING`,
- nepovoluj více aktivních identit bez takeover pravidla.

**Hotovo když**
- tým dostane teamId + reconnect token,
- reconnect state endpoint vrací snapshot.

**Ruční kontrola**
- duplicitní název týmu je odmítnut,
- neplatný join code je odmítnut.

---

## Krok S13 — Organizátorský waiting room a session create UI
**Cíl**  
Připravit UI a základní backend napojení pro waiting room.

**Uděláš**
- UI pro vytvoření session nad kvízem,
- zobrazení join code,
- waiting room obrazovku,
- seznam připojených týmů přes snapshot nebo dočasné pollování.

**Nesmíš měnit**
- zatím nefinalizuj game engine.

**Hotovo když**
- organizátor vidí novou WAITING session a připojené týmy.

**Ruční kontrola**
- po joinu týmu se waiting room aktualizuje nebo jde obnovit refreshí.

---

## Krok S14 — Start/cancel session backend
**Cíl**  
Zprovoznit řízené stavové přechody session.

**Uděláš**
- `POST /api/sessions/{sessionId}/start`
- `POST /api/sessions/{sessionId}/cancel`
- validace přechodů,
- podmínka min. 1 připojený tým pro start,
- potvrzovací požadavek pro cancel na UI vrstvě,
- audit log `SESSION_STARTED` a `SESSION_CANCELLED`.

**Nesmíš měnit**
- nepovoluj mutace terminálních stavů.

**Hotovo když**
- start přepne WAITING do RUNNING,
- cancel funguje v WAITING i RUNNING,
- terminální stavy jsou uzamčené.

**Ruční kontrola**
- start bez týmu je odmítnut,
- zrušení vyžaduje potvrzení v UI.

---

## Krok S15 — Otázkový engine a timeout progression
**Cíl**  
Dodat deterministický průběh hry řízený serverem.

**Uděláš**
- určení aktuální otázky,
- nastavení `CurrentQuestionIndex`, `CurrentQuestionStartedAtUtc`, `QuestionDeadlineUtc`,
- automatický přechod po timeoutu,
- finalizaci po poslední otázce.

**Nesmíš měnit**
- neposílej sekundové tikání ze serveru,
- nenechávej klienta rozhodovat o deadline.

**Hotovo když**
- server sám posouvá otázky,
- poslední otázka vede do `FINISHED`.

**Ruční kontrola**
- po vypršení času dojde k přechodu i bez zásahu klienta.

---

## Krok S16 — SignalR session groups a eventy
**Cíl**  
Napojit real-time vrstvu.

**Uděláš**
- session-specific skupiny,
- eventy pro team joined, session started, question changed, session cancelled, session finished, results ready,
- reconnect flow pro znovunapojení klienta.

**Nesmíš měnit**
- nepřenášej sekundové tick eventy.

**Hotovo když**
- waiting room a průběh session lze synchronizovat real-time.

**Ruční kontrola**
- dvě otevřená okna vidí změnu téměř okamžitě.

---

## Krok S17 — Team UI: join, waiting room, question screen
**Cíl**  
Dodat použitelné týmové rozhraní.

**Uděláš**
- join formulář,
- uložení team identity lokálně,
- waiting room týmu,
- question screen s odpověďmi A/B/C/D,
- jasné potvrzení po odeslání odpovědi.

**Nesmíš měnit**
- nezobrazuj správnou odpověď během hry,
- nepovol druhý submit téže otázky.

**Hotovo když**
- tým projde flow od joinu po otázku.

**Ruční kontrola**
- po odeslání se odpověď uzamkne a UI to jasně ukáže.

---

## Krok S18 — Answer submit backend
**Cíl**  
Doplnit backend pro odpovědi a pravidlo first-write-wins.

**Uděláš**
- `POST /api/sessions/{sessionId}/answers`,
- validaci stavu session,
- validaci deadline,
- unikátní answer per team/question,
- výpočet `IsCorrect` a `ResponseTimeMs`.

**Nesmíš měnit**
- nesmí projít duplicitní ani pozdní submit.

**Hotovo když**
- odpověď se uloží přesně jednou,
- pozdní/duplicitní odpovědi vrací správný business error.

**Ruční kontrola**
- rychlý dvojitý klik nezpůsobí dvě odpovědi.

---

## Krok S19 — Výsledky, ranking a correct answers
**Cíl**  
Zfinalizovat konec hry.

**Uděláš**
- výpočet `SessionResult`,
- ranking podle score a tie-breaku,
- endpoint pro finální výsledky,
- endpoint pro správné odpovědi po skončení,
- team results screen a organizer results screen.

**Nesmíš měnit**
- nezobrazuj výsledky ani správné odpovědi před `FINISHED`.

**Hotovo když**
- po skončení session existují finální výsledky,
- organizátor vidí i správné odpovědi.

**Ruční kontrola**
- při stejném score rozhoduje nižší součet časů správných odpovědí.

---

## Krok S20 — Hardening a bezpečnostní minimum
**Cíl**  
Doplnit nejnutnější odolnost.

**Uděláš**
- rate limiting,
- sanitizaci textových vstupů,
- dotažení hashování a constant-time compare,
- audit log minimum,
- HTTPS/TLS ready konfiguraci,
- ošetření reconnectu do 60 s.

**Nesmíš měnit**
- nerozšiřuj scope o enterprise funkce navíc.

**Hotovo když**
- základní bezpečnostní a provozní minima jsou implementována.

**Ruční kontrola**
- rate limit a reconnect chování jsou aspoň základně ověřené.

---

## Krok S21 — Testy a release readiness
**Cíl**  
Uzavřít MVP do stavu rozumně předatelného k dalšímu testu.

**Uděláš**
- unit testy pro kritická business pravidla,
- integrační testy tam, kde dávají největší hodnotu,
- závěrečné dočištění,
- kontrolu souladu s akceptačními kritérii,
- aktualizaci stavových souborů do finální podoby.

**Nesmíš měnit**
- nepouštěj se do nových feature nápadů.

**Hotovo když**
- kritické business invariants mají testové pokrytí,
- existuje finální checklist a projekt je konzistentní.

**Ruční kontrola**
- projdi celý happy path:
  create quiz -> import -> create session -> join -> start -> answer -> finish -> results.
