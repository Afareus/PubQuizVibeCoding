# .github sada pro AI‑řízený vývoj pub kvíz aplikace

Tato složka je navržená pro vývoj ve Visual Studiu s GitHub Copilotem tak, aby hlavní pracovní režim mohl být co nejjednodušší:

1. zkontroluješ výstup aktuálního kroku,
2. případně spustíš krátký ruční test,
3. napíšeš jen **„Pokračuj k dalšímu kroku“** nebo použiješ **`/continue`**.

## Co je uvnitř

- `copilot-instructions.md`  
  Trvalá pravidla pro každý chat a každou změnu v repozitáři.
- `context/`  
  Zdroj pravdy pro produkt, architekturu, roadmapu, workflow, rizika a průběžný stav.
- `instructions/`  
  Path-specific pravidla pro C#, Razor, EF Core, HTTP vrstvu a testy.
- `prompts/`  
  Znovupoužitelné prompt soubory pro pokračování, review, opravy a finální stabilizaci.

## Doporučený způsob použití

### Běžný režim
Do Copilot chatu napiš:

```text
Pokračuj k dalšímu kroku
```

nebo použij:

```text
/continue
```

Agent má v tom případě:
1. přečíst `context/08-implementation-state.md`,
2. najít první nedokončený krok v `context/04-roadmap.md`,
3. provést pouze tento krok,
4. udělat build a relevantní testy,
5. aktualizovat stav, decision log a changelog,
6. zastavit se a předat ti stručný report.

### Když se něco rozbije
Použij:

```text
/repair
```

### Když chceš jen audit bez nových změn
Použij:

```text
/review
```

### Když se rozjede stav a dokumentace
Použij:

```text
/resync-state
```

## Důležitá poznámka
Prompt files jsou preview funkce, ale tento adresář je navržen tak, aby základní workflow fungovalo i bez nich. Primární logika je proto uložená v `copilot-instructions.md` a ve stavových/context souborech.

## Co musíš udělat ručně jen jednou
- otevřít repozitář ve Visual Studiu,
- mít zapnutý GitHub Copilot Chat a Agent mode,
- zkontrolovat, že Copilot používá custom instructions z `.github`,
- ponechat tuto složku v repozitáři od úplného začátku vývoje.

## Praktická rada
První zpráva v novém repozitáři může být rovnou:

```text
Pokračuj k dalšímu kroku
```

Protože roadmapa začíná bootstrapem řešení od nuly.
