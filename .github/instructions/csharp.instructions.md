---
applyTo: "**/*.cs"
---
# Pravidla pro C# kód

- Piš čitelný a konzistentní C#.
- Preferuj malé služby a explicitní business logiku.
- Nepřidávej patterny jen proto, že vypadají architektonicky zajímavě.
- Každou veřejnou metodu pojmenuj tak, aby byl zřejmý její účel.
- Časy zpracovávej v UTC.
- Dlouhé metody rozděluj na menší privátní části.
- U asynchronních metod používej `CancellationToken`, kde to dává smysl.
- U validačních/business chyb vracej konzistentní aplikační výsledek nebo HTTP error model podle vrstvy.
- Nikdy nevracej hashované tokeny ani hashovaná hesla do DTO.
- U session logiky mysli na souběh, idempotenci a first-write-wins.
- Komentáře přidávej jen tam, kde vysvětlují důvod, ne to, co je zřejmé ze syntaxe.
