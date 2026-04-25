# Rizika a anti-drift pravidla

## Největší riziko

Agent začne znovu stavět nebo refaktorovat hotovou Pub kvíz aplikaci místo toho, aby přidal malý samostatný Challenge mód.

Tomu se musí zabránit.

## Riziko: scope creep

Zakázané rozšíření v první verzi:
- vlastní otázky,
- AI generování,
- účty,
- profily,
- sociální login,
- veřejný katalog,
- platby,
- reklamy,
- administrace,
- mazání výsledků,
- moderace leaderboardu,
- obrázky a média.

První verze musí být úzká:

```text
10 pevných otázek → public link → hráč odpoví → skóre → leaderboard → vytvořit vlastní
```

## Riziko: únik správných odpovědí

Challenge detail pro hráče nesmí obsahovat tvůrcovy správné odpovědi.

Kontroluj:
- DTO,
- API response,
- client model,
- serializaci entit,
- AutoMapper/projection, pokud existuje.

## Riziko: rozbití live Pub kvízu

Nezasahuj do:
- session state machine,
- SignalR hubů,
- Organizer/Player flow,
- CSV importu,
- existujících API endpointů,
- existujících rout.

Pokud je zásah nutný kvůli sdílené infrastruktuře, musí být minimální a jasně vysvětlený.

## Riziko: příliš složitý datový model

Nepřidávej tabulky pro:
- uživatele,
- profily,
- šablony spravované v DB,
- marketing kampaně,
- tracking.

Pro MVP stačí entity uvedené v `02-architecture-and-data-model.md`.

## Riziko: slabá viralita

Challenge mód bez CTA není hotový.

Na výsledkové stránce musí být:
- skóre,
- leaderboard,
- sdílecí text,
- výrazné tlačítko `Vytvořit vlastní kvíz`.

## Riziko: špatné mobilní UX

Primární použití je mobil. Formuláře musí být krátké, tlačítka velká a texty jasné.

## Anti-drift věta

Když máš pochybnost, zvol menší řešení, které dokončí virální smyčku.
