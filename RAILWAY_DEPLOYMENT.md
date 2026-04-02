# Příprava a nasazení na Railway

Tento repozitář je připravený pro deploy backendu (`QuizApp.Server`) do Railway přes `Dockerfile` v rootu.

## Co je už připraveno

- Produkční Docker build + run (`Dockerfile`)
- Health endpoint pro Railway (`/health`)
- CORS čtené z konfigurace (`Cors:AllowedOrigins`)
- Produkční konfigurační soubory:
  - `QuizApp.Server/appsettings.Production.json`
  - `QuizApp.Client/wwwroot/appsettings.Production.json`

## Co doplnit po registraci na Railway

### 1) Backend service (`QuizApp.Server`)

V Railway vytvoř service z GitHub repozitáře a nastav build přes `Dockerfile`.

Nastav proměnné prostředí:

- `ASPNETCORE_ENVIRONMENT=Production`
- `PORT=8080` (Railway často nastavuje automaticky, ale je vhodné vědět, že app na ní poslouchá)
- `PostgreSql__ConnectionString=<RAILWAY_POSTGRES_CONNECTION_STRING>`
- `Cors__AllowedOrigins__0=<URL_FRONTENDU_1>`
- `Cors__AllowedOrigins__1=<URL_FRONTENDU_2>` (volitelné)

Healthcheck nastav na:

- Path: `/health`

### 2) Databáze

- Připoj PostgreSQL plugin/službu v Railway.
- Connection string vlož do `PostgreSql__ConnectionString`.
- Pokud Railway vrátí connection string s TLS parametry, použij ho beze změny.

Poznámka: aplikace při startu spouští EF migrace automaticky.

### 3) Frontend (`QuizApp.Client`)

Frontend může běžet zvlášť (další Railway service / static hosting).

Před produkčním buildem klienta nastav v `QuizApp.Client/wwwroot/appsettings.Production.json`:

- `ApiBaseUrl` na veřejnou URL backendu v Railway, např. `https://<backend>.up.railway.app`

Pokud klient poběží na jiné doméně než backend, tato doména musí být v backend CORS (`Cors__AllowedOrigins__*`).

## Lokální vývoj ve Visual Studiu

Lokální vývoj zůstává beze změny:

- `QuizApp.Server/appsettings.Development.json` obsahuje localhost DB + CORS
- `QuizApp.Client/wwwroot/appsettings.json` obsahuje localhost API URL
- `launchSettings.json` pro oba projekty zůstává pro `Development`

## Doporučení pro první produkční spuštění

- Nechat 1 instanci backendu (kvůli SignalR a in-memory stavu)
- Po deployi zkontrolovat:
  - `/health` vrací 200
  - vytvoření kvízu
  - připojení týmu do session
  - průchod jedním kolem otázek
