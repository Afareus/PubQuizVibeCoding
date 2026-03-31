# Produktová specifikace MVP

## Role

### Organizátor
Umí:
- založit nový kvíz,
- nastavit Administrátorké heslo kvízu,
- nahrát CSV s otázkami,
- zobrazit detail kvízu,
- založit novou session,
- získat join code,
- sledovat waiting room,
- ručně spustit session,
- zrušit session po potvrzení,
- po skončení zobrazit finální výsledky a správné odpovědi,
- logicky smazat kvíz po zadání správného hesla.

### Tým
Umí:
- zadat join code,
- zvolit unikátní název týmu,
- připojit se do WAITING session,
- po startu odpovídat na aktuální otázku,
- odeslat právě jednu odpověď na otázku,
- po refreshi obnovit stav přes reconnect token,
- po skončení vidět finální pořadí.

## Hlavní business pravidla
- Session lze vytvořit jen nad kvízem s alespoň 1 validní otázkou.
- Start session je povolen pouze ve stavu `WAITING`.
- Start session je povolen pouze organizátorovi s platným tokenem.
- Start session je povolen jen tehdy, když je připojen alespoň 1 tým.
- Join týmu je povolen jen ve stavu `WAITING`.
- Duplicitní název týmu v rámci jedné session je zakázán.
- Po startu session se další týmy nepřipojují.
- Odpověď lze odeslat jen jednou pro daný tým a otázku.
- Platí pravidlo `first-write-wins`.
- Odpověď po timeoutu je neplatná.
- Server po timeoutu automaticky přechází na další otázku.
- Po poslední otázce server jednou finalizuje výsledky a session přejde do `FINISHED`.
- `FINISHED` a `CANCELLED` jsou terminální stavy.
- Zrušení session v běhu okamžitě ukončí hru; průběžné výsledky nejsou oficiálním finálním pořadím.
- Bodování:
  - správně = 1 bod,
  - jinak = 0 bodů.
- Tie-break:
  - nižší součet časů správných odpovědí vyhrává.

## Dashboard a seznam kvízů
- V MVP neexistuje serverový list endpoint pro všechny kvízy.
- „Seznam kvízů“ je klientsky sestavený lokální seznam dostupný pouze z aktuálního browseru.
- Klient jej skládá z lokálně uložených dvojic `quizId + QuizOrganizerToken`.

## UX pravidla
- UI musí být v češtině.
- Mobilní použití musí být pohodlné a přehledné.
- Během hry nesmí docházet k rušivému přeskakování layoutu.
- Po odeslání odpovědi musí být stav jasně potvrzen.
- Během hry nesmí být omylem zobrazeny správné odpovědi ani průběžné výsledky.

## Chybové stavy, které musí existovat
- neplatný join code
- duplicitní název týmu
- pozdní odpověď
- duplicitní submit
- chybné Administrátorké heslo kvízu
- chybná hlavička CSV
- nevalidní `correct_option`
- nevalidní `time_limit_sec`
