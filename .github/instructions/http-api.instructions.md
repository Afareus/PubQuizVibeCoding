---
applyTo: "**/Controllers/**/*.cs,**/Endpoints/**/*.cs,**/*Endpoint*.cs,**/*Controller*.cs,**/*Request*.cs,**/*Response*.cs"
---
# Pravidla pro HTTP API

- Endpointy drž tenké; business logika patří do služeb.
- Použij existující styl API v repozitáři.
- Challenge endpointy jsou veřejné, protože fungují přes sdílený odkaz.
- Nepřidávej login ani Identity.
- `GET /api/challenges/{publicCode}` nesmí vracet správné odpovědi tvůrce.
- `POST /api/challenges/{publicCode}/submissions` skóruje odpovědi na serveru.
- Použij konzistentní chybový model aplikace.
- Validuj délky textů a povinná pole.
- Neměň existující Pub kvíz API endpointy, pokud to není nutné.
