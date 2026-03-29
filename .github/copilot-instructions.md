# Repo-wide instrukce pro AI vývoj pub kvíz aplikace

## 1. Zdroj pravdy a priorita dokumentů
Nejvyšší prioritu mají tyto soubory v tomto pořadí:

1. `context/00-source-of-truth.md`
2. `context/01-product-spec.md`
3. `context/02-architecture-and-data-model.md`
4. `context/03-api-signalr-and-security.md`
5. `context/04-roadmap.md`
6. `context/05-workflow-rules.md`
7. `context/06-risks-and-anti-drift.md`
8. `context/08-implementation-state.md`
9. `context/09-decision-log.md`

Pokud narazíš na konflikt, zastav se u souboru s vyšší prioritou. Pokud dokumentace něco neřeší, zvol nejjednodušší řešení konzistentní s úzkým MVP, uveď to jako doplněný návrh a zapiš to do `context/09-decision-log.md`.

## 2. Význam uživatelských příkazů
Když uživatel napíše pouze nebo převážně jednu z následujících vět, interpretuj ji takto:

- **„Pokračuj k dalšímu kroku“**  
  = proveď přesně jeden další nedokončený krok z roadmapy.
- **„Zkontroluj aktuální krok“**  
  = neprováděj nové feature změny, jen audit souladu, build, testy a případné drobné opravy nutné pro dokončení aktuálního kroku.
- **„Oprav chyby a pokračuj“**  
  = nejprve oprav aktuální rozbitý stav, ale nepřeskakuj do dalšího kroku, dokud není stávající krok opravdu hotový.
- **„Shrň stav“**  
  = bez implementace přečti stavové soubory a dej stručný report.

Jestliže uživatel nenapíše nic dalšího než „Pokračuj k dalšímu kroku“, nevyžaduj doplňující informace, pokud tomu nebrání přímý rozpor ve specifikaci.

## 3. Hlavní pracovní režim
Při příkazu pokračovat vždy postupuj takto:

1. Přečti `context/08-implementation-state.md`.
2. Přečti odpovídající část v `context/04-roadmap.md`.
3. Přečti relevantní soubory v `context/` a `instructions/` pro daný krok.
4. Proveď **jen jeden** krok, ne více.
5. Udrž změny malé, soudržné a reverzibilní.
6. Po implementaci proveď build a relevantní testy.
7. Aktualizuj:
   - `context/08-implementation-state.md`
   - `context/09-decision-log.md`
   - `context/10-changelog.md`
8. Zastav se. Nepřecházej do dalšího kroku v tomtéž tahu.

## 4. Co nesmíš dělat
Nikdy bez výslovného pokynu:
- nerozšiřuj scope mimo MVP,
- nepřidávej login, Identity, role management, e-mailové účty ani správu uživatelů,
- nepřidávej editaci otázek v UI,
- nepřidávej open-ended odpovědi, multi-choice, obrázky, audio ani video,
- nepřidávej průběžný leaderboard ani správné odpovědi během hry,
- neměň stack na jinou databázi nebo jiný frontend model,
- nepředělávej architekturu na mikroservisy,
- nenasazuj patterny jen proto, že „jsou enterprise“,
- neskrývej nehotový krok jako hotový,
- nepřeskakuj testování a aktualizaci stavových souborů.

## 5. Požadovaný styl implementace
- Preferuj jednoduché a čitelné řešení před frameworkovou složitostí.
- Zachovej modulární monolit.
- Server je autorita nad časem, session stavem a pořadím otázek.
- Všechny časy ukládej a porovnávej v UTC.
- Hesla a tokeny nikdy neukládej čitelně.
- Nepoužívej provider-specific zkratky, které zhorší přenositelnost nebo čitelnost, pokud nejsou nezbytné.
- U business pravidel preferuj explicitní aplikační služby a malé, dobře pojmenované metody.
- U každé změny mysli na reconnect, souběh a determinismus.
- Používej nejnovější dostupné verze závislostí/nástrojů, pokud jsou kompatibilní (např. PostgreSQL 18).

## 6. Definition of Done pro každý krok
Krok je hotový jen tehdy, když:
- odpovídá specifikaci,
- build projde,
- relevantní testy projdou,
- ruční smoke-check popsaný v roadmapě dává smysl a je zaznamenán,
- změna je zapsaná do stavového souboru,
- nejsou otevřené TODO poznámky, které blokují další krok.

Pokud build nebo relevantní testy nejdou spustit kvůli chybějícímu prostředí, uveď přesně co nešlo ověřit a proč. I v takovém případě aktualizuj stav poctivě.

## 7. Požadovaný formát výstupu po každém kroku
Na konci implementačního tahu vypiš přesně tyto sekce:

### Hotový krok
Identifikátor a název kroku.

### Co bylo změněno
Krátký seznam souborů nebo oblastí.

### Ověření
- build
- testy
- případné limity ověření

### Ruční kontrola pro uživatele
1 až 3 konkrétní body.

### Stav
Co je nyní hotové a jaký je další krok podle roadmapy.

## 8. Jak řešit nejistotu
Pokud něco není výslovně rozhodnuto:
- zvol nejjednodušší variantu konzistentní s MVP,
- neptej se na preference, které neblokují vývoj,
- zapiš rozhodnutí do `context/09-decision-log.md`,
- pokračuj.

Ptej se jen tehdy, pokud by dvě různé varianty vedly k zásadně odlišné architektuře nebo porušení zdroje pravdy.

## 9. Jak zacházet se stavovými soubory
`context/08-implementation-state.md`, `context/09-decision-log.md` a `context/10-changelog.md` jsou provozní paměť repozitáře. Při každém kroku je aktualizuj. Bez jejich aktualizace nepovažuj krok za dokončený.

## 10. Specifické uživatelské preference
- Tlačítko 'Zobrazit výsledky' na týmové obrazovce po konci hry zůstane viditelné a bude do zveřejnění výsledků organizátorem pouze disablované, nikoli skryté.
- Na týmové obrazovce po konci kvízu bude hláška 'Výsledky zatím nebyly zveřejněny organizátorem.' umístěna nad tlačítkem 'Zobrazit výsledky'.
