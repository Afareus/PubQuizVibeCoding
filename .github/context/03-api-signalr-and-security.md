# API, SignalR a bezpečnost pro Challenge mód

## REST endpointy

Challenge mód má používat REST API. SignalR není potřeba.

### Veřejné endpointy

```text
GET  /api/challenges/template
POST /api/challenges
GET  /api/challenges/{publicCode}
POST /api/challenges/{publicCode}/submissions
GET  /api/challenges/{publicCode}/leaderboard
```

Volitelné, pokud implementace potřebuje detail konkrétního výsledku:

```text
GET /api/challenges/{publicCode}/submissions/{submissionId}
```

## Endpoint: GET /api/challenges/template

Vrací pevnou sadu 10 šablonových otázek včetně 4 možností.

Nesmí vracet žádné správné odpovědi, protože šablona sama o sobě správné odpovědi nemá. Správnou odpovědí je až volba tvůrce.

## Endpoint: POST /api/challenges

Vytvoří challenge z odpovědí tvůrce.

Validace:
- `creatorName` povinné,
- `title` povinné nebo automaticky doplněné,
- přesně 10 odpovědí,
- každá odpověď musí odkazovat na existující šablonovou otázku,
- každá odpověď musí mít `selectedOptionKey` z možností A-D,
- žádná otázka nesmí chybět ani být duplicitní.

Výsledek:
- uloží challenge,
- uloží otázky,
- uloží možnosti,
- uloží tvůrcovy správné odpovědi,
- vrátí `publicCode`.

## Endpoint: GET /api/challenges/{publicCode}

Vrátí challenge pro hraní.

Nesmí vracet:
- `CreatorSelectedOptionKey`,
- `IsCorrect`,
- hashe tokenů,
- interní bezpečnostní údaje.

## Endpoint: POST /api/challenges/{publicCode}/submissions

Odešle hráčovy odpovědi.

Validace:
- challenge existuje a není smazaná,
- `participantName` je povinné,
- přesně jedna odpověď na každou otázku,
- žádná otázka nesmí chybět ani být duplicitní,
- selected option musí existovat u dané otázky.

Server:
- porovná odpovědi s `CreatorSelectedOptionKey`,
- uloží submission,
- uloží detail odpovědí,
- spočítá `score`,
- vrátí skóre, rank a leaderboard.

## Endpoint: GET /api/challenges/{publicCode}/leaderboard

Vrátí top výsledky.

Pravidla:
- řadit podle skóre sestupně,
- při shodě řadit podle `SubmittedAtUtc` vzestupně,
- pro MVP stačí top 20.

## Chybový model

Použij existující chybový model aplikace. Pokud žádný jednotný model není, drž jednoduché konzistentní chyby.

Doporučené chyby:
- `ChallengeNotFound`
- `ChallengeDeleted`
- `InvalidChallengeTemplateAnswer`
- `MissingParticipantName`
- `InvalidSubmissionAnswerCount`
- `DuplicateSubmissionAnswer`
- `UnknownChallengeQuestion`
- `UnknownChallengeOption`
- `ValidationFailed`

## Bezpečnostní zásady

- Správné odpovědi tvůrce nikdy neposílat klientovi před odesláním odpovědí.
- Nevracet hashovaná pole do DTO.
- Validovat délku a obsah jmen.
- Zamezit extrémně dlouhým vstupům.
- Public challenge je z principu sdílená přes odkaz, ne soukromá.
- Nepřidávat login, Identity ani sociální přihlášení.
- Nepřidávat e-mailové účty.
- Pro první verzi nepřidávat cookies ani komplexní tracking.

## SignalR

Challenge mód nepoužívá SignalR.

Existující SignalR kód pro live Pub kvíz neměnit, pokud to není nutné kvůli buildu.
