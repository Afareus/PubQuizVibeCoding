---
applyTo: "**/*.cs"
---
# Pravidla pro C# kód

- Piš čitelný a konzistentní C# podle existujícího stylu projektu.
- Preferuj malé služby a explicitní business logiku.
- Nepřidávej patterny jen proto, že vypadají architektonicky zajímavě.
- Časy zpracovávej v UTC.
- U asynchronních metod používej `CancellationToken`, kde to dává smysl.
- Nikdy nevracej hashované tokeny ani hashovaná hesla do DTO.
- Pro Challenge mód nikdy neposílej správné odpovědi tvůrce před odesláním hráčovy submission.
- U scoringu používej server jako jedinou autoritu.
- Dlouhé metody rozděl na menší privátní části.
- Komentáře přidávej jen tam, kde vysvětlují důvod, ne zřejmou syntaxi.
- Neměň existující live Pub kvíz logiku, pokud to není nutné pro nový krok.
