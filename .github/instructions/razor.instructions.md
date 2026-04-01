---
applyTo: "**/*.razor,**/*.razor.cs,**/*.css"
---
# Pravidla pro Razor a UI

- UI je v češtině.
- Upřednostni mobilní použitelnost.
- Během hry udržuj layout stabilní; nevyvolávej rušivé přeskakování obrazovky.
- Po odeslání odpovědi musí být stav zřetelně potvrzen.
- Nikdy nezobrazuj správné odpovědi během `WAITING` ani `RUNNING`, kromě organizátorské obrazovky v režimu bez časomíry po explicitním kliknutí na tlačítko pro zobrazení správné odpovědi aktuální otázky.
- Nikdy nezobrazuj průběžný leaderboard během hry.
- Organizátorské a týmové obrazovky drž oddělené a srozumitelné.
- Formuláře drž jednoduché a s jasnými chybovými hláškami v češtině.
- Potvrzovací dialog pro zrušení session je povinný.
