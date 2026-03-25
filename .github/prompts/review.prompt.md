---
agent: 'agent'
description: 'Zkontroluj aktuální krok proti specifikaci, buildům a stavovým souborům bez rozšiřování scope'
---

Proveď audit aktuálního stavu repozitáře.

Nejdřív přečti:
- `.github/context/00-source-of-truth.md`
- `.github/context/04-roadmap.md`
- `.github/context/08-implementation-state.md`
- `.github/context/06-risks-and-anti-drift.md`

Pak:
1. určuj, který krok je rozpracovaný nebo naposledy dokončený,
2. zkontroluj soulad se specifikací,
3. spusť build a relevantní testy, pokud je to možné,
4. oprav jen drobné chyby nutné k dokončení stejného kroku,
5. neimplementuj nový další krok,
6. aktualizuj stavové soubory, pokud zjistíš nesoulad.

Na konci vypiš:
- Stav kroku
- Nalezené problémy
- Co bylo opraveno
- Co má uživatel zkontrolovat
