# Ruční smoke testy

Tento soubor slouží jako rychlý checklist pro člověka po každém důležitém kroku.

## Základní smoke testy podle oblasti

### Kvízy
- lze vytvořit nový kvíz,
- organizer token se ukáže jen jednou,
- lokální dashboard po refreshi stále obsahuje dříve uložený kvíz,
- detail kvízu se načte jen s validním tokenem,
- smazání vyžaduje heslo.

### CSV import
- validní CSV projde,
- nevalidní CSV vrátí chybu s řádkem, sloupcem a důvodem,
- prázdné řádky se ignorují,
- bez otázek nelze session vytvořit.

### Session
- session se vytvoří ve stavu WAITING,
- join code se zobrazí organizátorovi,
- tým se může připojit jen ve WAITING,
- po startu už další tým nejoinne,
- cancel funguje jen po potvrzení.

### Tým
- duplicitní název týmu je odmítnut,
- po refreshi se obnoví stav,
- odpověď jde poslat jen jednou,
- po odeslání je stav zřetelně uzamčen.

### Hra
- countdown běží klientsky,
- server po timeoutu přepne otázku,
- během RUNNING nejsou vidět výsledky ani správné odpovědi,
- po poslední otázce se zobrazí finální výsledky.

### Výsledky
- score je správně,
- tie-break vyhodnotí nižší součet časů správných odpovědí,
- organizátor po FINISHED vidí správné odpovědi,
- tým vidí jen finální pořadí.

## Závěrečný end-to-end smoke test
1. Založ nový kvíz.
2. Nahraj validní CSV.
3. Založ novou session.
4. Připoj alespoň 2 týmy.
5. Spusť session.
6. Odešli odpovědi.
7. Počkej na konec.
8. Ověř finální pořadí a správné odpovědi pro organizátora.
9. Zkus logické smazání kvízu po ukončení všech aktivních session.
