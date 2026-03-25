---
applyTo: "**/*Tests*/**/*.cs,**/*.Tests/**/*.cs"
---
# Pravidla pro testy

- Preferuj xUnit.
- Testuj hlavně business pravidla, ne kosmetické detaily.
- Každý test má mít jedno jasné očekávání.
- Kritické scénáře:
  - create session bez otázek je zakázán,
  - join mimo `WAITING` je zakázán,
  - duplicitní týmové jméno je zakázáno,
  - first-write-wins pro odpovědi,
  - pozdní odpověď je odmítnuta,
  - `FINISHED` a `CANCELLED` jsou terminální,
  - delete quiz při aktivní session je zakázán,
  - výsledky a correct answers nejsou dostupné před `FINISHED`.
- U testů používej názvy, ze kterých je okamžitě patrné pravidlo a očekávaný výsledek.
