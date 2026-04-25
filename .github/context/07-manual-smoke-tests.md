# Manuální smoke testy pro Challenge MVP

## A. Ověření původní aplikace

Po dokončení větších změn krátce ověř:
- existující Pub kvíz aplikace se spustí,
- existující hlavní navigace funguje,
- Organizer/Player flow není zjevně rozbitý.

Detailní retest starého Pub kvízu není cílem každého kroku, ale nesmí být zjevně poškozený.

## B. Vytvoření challenge

1. Otevři `/challenge/create`.
2. Zadej jméno tvůrce.
3. Ponech nebo uprav název challenge.
4. Vyber odpověď u všech 10 otázek.
5. Klikni na `Vytvořit challenge`.

Očekávání:
- challenge se uloží,
- zobrazí se veřejný odkaz,
- zobrazí se sdílecí text,
- odkaz obsahuje `publicCode`.

## C. Hraní challenge

1. Otevři veřejný odkaz v anonymním okně nebo jiném prohlížeči.
2. Zadej jméno hráče.
3. Odpověz na všech 10 otázek.
4. Odešli odpovědi.

Očekávání:
- submission se uloží,
- zobrazí se skóre z 10,
- nezobrazila se chyba,
- správné odpovědi nebyly dostupné před odesláním.

## D. Leaderboard

1. Odehraj stejnou challenge alespoň dvěma různými jmény.
2. Zkontroluj leaderboard.

Očekávání:
- výsledky jsou seřazené podle skóre sestupně,
- při shodě dřívější submission vyhrává,
- zobrazuje se top 20 nebo méně.

## E. Virální CTA

Po dokončení challenge ověř:
- je vidět tlačítko `Vytvořit vlastní kvíz`,
- tlačítko vede na `/challenge/create`,
- sdílecí text obsahuje skóre a odkaz.

## F. Negativní scénáře

Ověř alespoň ručně nebo testem:
- neexistující `publicCode` vrátí srozumitelnou chybu,
- nejde odeslat odpovědi bez jména,
- nejde odeslat nekompletní odpovědi,
- nejde odeslat neexistující option key,
- detail challenge pro hráče neobsahuje správné odpovědi.
