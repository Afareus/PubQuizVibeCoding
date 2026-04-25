---
applyTo: "**/*Tests*/**/*.cs,**/*.Tests/**/*.cs"
---
# Pravidla pro testy

- Preferuj xUnit, pokud ho projekt používá.
- Testuj hlavně business pravidla.
- Každý test má mít jedno jasné očekávání.
- U Challenge módu testuj hlavně:
  - vytvoření challenge s přesně 10 odpověďmi,
  - odmítnutí chybějící odpovědi,
  - odmítnutí duplicitní odpovědi na stejnou otázku,
  - odmítnutí neexistující option key,
  - detail challenge pro hráče neobsahuje správné odpovědi,
  - submission spočítá správné skóre,
  - leaderboard řadí podle skóre sestupně a času vzestupně,
  - neexistující public code vrací srozumitelnou chybu.
- Netestuj kosmetické detaily UI, pokud nejsou kritické pro flow.
