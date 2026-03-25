---
applyTo: "**/Controllers/**/*.cs,**/Endpoints/**/*.cs,**/*Endpoint*.cs,**/*Controller*.cs,**/*Request*.cs,**/*Response*.cs"
---
# Pravidla pro HTTP API

- Organizátorské operace kromě `POST /api/quizzes` vyžadují `X-Organizer-Token`.
- Týmové operace po joinu vyžadují `X-Team-Reconnect-Token`.
- Dodržuj error model ze specifikace.
- Správné odpovědi nesmí být vráceny před `FINISHED`.
- Výsledky nesmí být vráceny jako oficiální finální pořadí před `FINISHED`.
- Endpointy musí být tenké; business logika patří do služeb.
- U stavových přechodů kontroluj stav session explicitně.
