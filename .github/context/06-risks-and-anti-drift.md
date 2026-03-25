# Rizika AI vývoje a prevence driftu

## Nejčastější chyby, kterým musíš předcházet

### 1. Přepnutí na jiný produkt než úzké MVP
Riziko:
- AI začne přidávat účty, správu uživatelů, editaci otázek, administraci nebo širší SaaS model.

Prevence:
- vždy znovu zkontroluj `00-source-of-truth.md`,
- drž se jen aktuálního kroku z roadmapy.

### 2. Špatný Blazor model
Riziko:
- AI zvolí server-side interaktivitu jako hlavní model místo klienta ve WebAssembly režimu.

Prevence:
- respektuj zdroj pravdy: klient ve WASM režimu, server jako API + SignalR.

### 3. Zobrazení zakázaných dat během hry
Riziko:
- AI omylem vrátí correct answers nebo průběžné výsledky dříve.

Prevence:
- každé DTO a endpoint kontroluj podle pravidla:
  - během `WAITING` a `RUNNING` nic z toho nesmí ven.

### 4. Slabé zacházení s tokeny a hesly
Riziko:
- AI uloží token nebo heslo v plain textu, loguje ho nebo ho vrací opakovaně.

Prevence:
- token se vrací jen jednou,
- na serveru se ukládá jen hash,
- logy nesmí obsahovat tajné hodnoty.

### 5. Nedeterministický session engine
Riziko:
- klient rozhoduje o času,
- více paralelních požadavků rozbije stav.

Prevence:
- server je autorita,
- session mutace musí respektovat concurrency,
- answer submit musí být first-write-wins.

### 6. Přehnaná architektura
Riziko:
- AI zavede CQRS, MediatR, event sourcing nebo jinou složitost bez reálné potřeby.

Prevence:
- preferuj jednoduché aplikační služby a čisté vrstvy.

### 7. Příliš velké kroky
Riziko:
- AI udělá tři roadmap kroky najednou.

Prevence:
- dokončuj jen první nedokončený krok,
- na konci každého kola se zastav.

### 8. Neudržovaný stavový soubor
Riziko:
- AI zapomene aktualizovat stav a příští kolo naváže špatně.

Prevence:
- krok není hotový bez aktualizace `08-implementation-state.md`.

### 9. Slabé testování business pravidel
Riziko:
- UI funguje, ale pravidla jako duplicate team name, late answer, first-write-wins nebo delete with active session nejsou opravdu chráněna.

Prevence:
- kritická business pravidla testuj co nejdřív po zavedení příslušné logiky.

## Anti-drift checklist před ukončením každého kola
- Je krok opravdu jen jeden?
- Je změna v souladu se specifikací?
- Neunikají správné odpovědi nebo výsledky dřív?
- Neunikají tokeny nebo hesla?
- Build prošel?
- Stavové soubory byly aktualizovány?
