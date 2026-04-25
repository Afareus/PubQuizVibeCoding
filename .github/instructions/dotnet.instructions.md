---
applyTo: "**/*.sln,**/*.csproj,**/*.props,**/*.targets,**/*.json"
---
# Pravidla pro .NET řešení a projektové soubory

- Zachovej `.NET 8`, pokud repozitář už explicitně nepoužívá jinou kompatibilní konfiguraci.
- Zachovej existující názvy projektů a solution strukturu.
- Nepřidávej balíčky bez jasného důvodu.
- Preferuj built-in možnosti .NET a ASP.NET Core.
- Nepřidávej Identity, autentizační balíčky ani ORM alternativy.
- Tajné údaje nikdy nehardcoduj.
- Při přidání balíčku vysvětli důvod v reportu.
- Pro Challenge MVP není potřeba nový projekt, mikroservisa ani samostatný frontend.
