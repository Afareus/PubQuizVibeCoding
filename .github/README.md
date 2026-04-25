# .github sada pro pokračování vývoje: virální Challenge mód

Tato složka je připravená pro Visual Studio + GitHub Copilot/Codex Agent tak, aby šlo navázat na hotovou Pub kvíz aplikaci a pokračovat novou funkcí:

**virální asynchronní Challenge mód „Kdo mě zná nejlíp?“**

Původní live Pub kvíz aplikace se bere jako hotová. Tyto instrukce už nevedou agenta k budování původního MVP od nuly.

## Co je uvnitř

- `copilot-instructions.md`  
  Trvalá pravidla pro agenta při každé změně.
- `context/`  
  Zdroj pravdy pro nový Challenge mód, architekturu, roadmapu, stav a rizika.
- `instructions/`  
  Path-specific pravidla pro C#, Razor, EF Core, HTTP API a testy.
- `prompts/`  
  Prompt soubory pro pokračování, audit, opravy a srovnání dokumentace se skutečným kódem.

## Doporučený způsob použití

V Copilot/Codex Agent chatu napiš:

```text
Pokračuj k dalšímu kroku
```

nebo použij:

```text
/continue
```

Agent má:
1. přečíst `.github/context/08-implementation-state.md`,
2. najít první nedokončený krok v `.github/context/04-roadmap.md`,
3. provést pouze tento krok,
4. ověřit build a relevantní testy,
5. aktualizovat stav, decision log a changelog,
6. zastavit se a předat stručný report.

## Důležité

Tyto soubory záměrně odstraňují většinu detailů původního Pub kvíz MVP, protože aplikace už je hotová.

Pro existující funkce platí:

```text
Skutečný kód je zdroj pravdy.
Nerozbíjet, nepřepisovat, nereimplementovat.
```

Pro novou funkci platí:

```text
Dokumentace v .github/context je zdroj pravdy.
Implementovat postupně podle roadmapy.
```
