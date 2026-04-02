# Příprava a nasazení na Railway

Tento repozitář je připravený pro deploy na Railway přes dva Dockerfile:

- `QuizApp.Server/Dockerfile`
- `QuizApp.Client/Dockerfile`

## Co je už připraveno

- Produkční Docker build + run pro server i client
- Health endpoint pro Railway (`/health`)
- CORS čtené z konfigurace (`Cors:AllowedOrigins`)
- Produkční konfigurační soubory:
  - `QuizApp.Server/appsettings.Production.json`
  - `QuizApp.Client/wwwroot/appsettings.Production.json`

## Co doplnit po registraci na Railway

### 1) Backend service (`QuizApp.Server`)

V Railway vytvoř service z GitHub repozitáře a nastav Dockerfile path na:

- `QuizApp.Server/Dockerfile`

Nastav proměnné prostředí:

- `ASPNETCORE_ENVIRONMENT=Production`
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

### 3) Frontend service (`QuizApp.Client`)

V Railway vytvoř druhou service ze stejného repozitáře a nastav Dockerfile path na:

- `QuizApp.Client/Dockerfile`

Před deployem klienta nastav v `QuizApp.Client/wwwroot/appsettings.Production.json`:

- `ApiBaseUrl` na veřejnou URL backendu v Railway, např. `https://<backend>.up.railway.app`

Po prvním deployi klienta přidej jeho veřejnou URL do backend CORS (`Cors__AllowedOrigins__*`).

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

## Rychlý postup (co nejjednodušší)

1. V Railway vytvoř backend service (`QuizApp.Server/Dockerfile`).
2. Přidej PostgreSQL a nastav `PostgreSql__ConnectionString`.
3. Deploy backendu a ověř `/health`.
4. V `QuizApp.Client/wwwroot/appsettings.Production.json` nastav `ApiBaseUrl` na backend URL.
5. V Railway vytvoř frontend service (`QuizApp.Client/Dockerfile`).
6. Po deployi frontendu přidej frontend URL do backend CORS.
7. Redeploy backendu a proveď smoke test.
