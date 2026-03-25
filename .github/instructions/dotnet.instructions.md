---
applyTo: "**/*.sln,**/*.csproj,**/*.props,**/*.targets,**/*.json"
---
# Pravidla pro .NET řešení a projektové soubory

- Drž názvy projektů:
  - `QuizApp.Client`
  - `QuizApp.Server`
  - `QuizApp.Shared`
  - `QuizApp.Tests`
- Cílový framework drž na `.NET 8`.
- Nepřidávej balíčky bez jasného důvodu.
- Preferuj built-in možnosti .NET a ASP.NET Core.
- Nepřidávej Identity, autentizační balíčky ani ORM alternativy.
- Konfiguraci dělej čitelně a minimálně.
- Tajné údaje nikdy nehardcoduj.
- Při přidání balíčku vždy zvaž, zda není možné stejného cíle dosáhnout vestavěnou funkcionalitou.
