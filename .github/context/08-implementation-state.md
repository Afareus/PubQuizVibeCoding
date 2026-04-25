# Implementační stav

Datum resetu dokumentace: 2026-04-25

## Shrnutí

Původní Pub kvíz aplikace je považovaná za hotovou. Tato dokumentace byla zredukována a přesměrována na další vývoj: **virální Challenge mód**.

## Aktuální strategický stav

```text
Hotovo:
- existující Pub kvíz aplikace

Nově se má implementovat:
- Challenge mód „Kdo mě zná nejlíp?“
```

## Stav roadmapy

| Krok | Stav | Poznámka |
|---|---:|---|
| CH-01 Datový model a EF Core migrace | ✅ Hotovo | Entity vytvořeny, migrace `AddChallengeMode` vygenerována |
| CH-02 Shared DTO a validační kontrakty | Nezahájeno | Čeká na CH-01 |
| CH-03 Challenge aplikační služba | Nezahájeno | Čeká na CH-02 |
| CH-04 HTTP API endpointy | Nezahájeno | Čeká na CH-03 |
| CH-05 UI pro vytvoření challenge | Nezahájeno | Čeká na CH-04 |
| CH-06 UI pro hraní challenge | Nezahájeno | Čeká na CH-04 |
| CH-07 Výsledek, leaderboard a virální CTA | Nezahájeno | Čeká na CH-06 |
| CH-08 Vstup do Challenge módu z aplikace | Nezahájeno | Čeká na CH-07 |
| CH-09 Stabilizace, testy a release checklist | Nezahájeno | Finální krok |

## První další krok

```text
CH-02 — Shared DTO a validační kontrakty
```

Agent má začít tímto krokem a nemá implementovat UI ani API endpointy ve stejném kroku.

## Poznámky pro agenta

- Staré soubory o budování Pub kvízu od nuly byly záměrně odstraněny nebo nahrazeny.
- Pokud v kódu existují funkce, které tato dokumentace nepopisuje, neodstraňuj je.
- Pokud dokumentace neodpovídá skutečnému stavu staré aplikace, ber kód jako zdroj pravdy.
- Pro Challenge mód se řiď touto dokumentací.
