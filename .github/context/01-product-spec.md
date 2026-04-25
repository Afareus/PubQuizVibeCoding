# Produktová specifikace: Challenge MVP

## Cíl funkce

Vytvořit jednoduchý virální režim, který se může šířit sám:

```text
Vytvořím osobní kvíz → pošlu odkaz → přátelé hrají → vidí skóre → vytvoří vlastní kvíz
```

Challenge mód má sloužit jako akviziční kanál pro aplikaci, ne jako náhrada live Pub kvízu.

## Role

### Tvůrce challenge

Umí:
- otevřít stránku pro vytvoření challenge,
- zadat své jméno,
- zadat nebo potvrdit název challenge,
- odpovědět na 10 předpřipravených otázek o sobě,
- vytvořit challenge,
- získat veřejný sdílecí odkaz,
- sdílet text s odkazem.

Nemusí:
- vytvářet účet,
- zakládat live session,
- nahrávat CSV,
- spouštět hru,
- být přítomen během hraní ostatních.

### Hráč / účastník

Umí:
- otevřít veřejný odkaz,
- vidět název challenge a jméno tvůrce,
- zadat své jméno,
- odpovědět na 10 otázek,
- odeslat odpovědi,
- vidět své skóre,
- vidět leaderboard,
- vytvořit vlastní challenge přes CTA.

## Uživatelský tok

### Vytvoření challenge

1. Uživatel otevře `/challenge/create`.
2. Zadá jméno.
3. Aplikace nabídne název, například:
   - `Jak dobře znáš Gabriela?`
4. Uživatel projde 10 otázek.
5. U každé otázky vybere svou odpověď.
6. Klikne na `Vytvořit challenge`.
7. Aplikace uloží challenge.
8. Aplikace zobrazí veřejný odkaz a tlačítka / text pro sdílení.

### Hraní challenge

1. Hráč otevře `/challenge/{publicCode}`.
2. Zadá své jméno.
3. Vyplní 10 odpovědí.
4. Odešle odpovědi.
5. Server spočítá skóre.
6. Hráč vidí výsledek, leaderboard a CTA `Vytvořit vlastní kvíz`.

## Otázky v první šabloně

První MVP používá pevnou sadu 10 otázek. Přesné texty může implementace upravit, ale musí zachovat princip osobního hádacího kvízu.

Doporučená sada:

1. Jaké jídlo bych si nejraději dal/a?
2. Kam bych nejraději vyrazil/a na dovolenou?
3. Co bych dělal/a s volným milionem korun?
4. Jaký typ filmu mám nejraději?
5. Jakou aktivitu bych si vybral/a na volný večer?
6. Co mě nejspíš nejvíc potěší?
7. Jaký nápoj bych si nejčastěji vybral/a?
8. Jak bych nejraději trávil/a víkend?
9. Co mě nejvíc vystihuje?
10. Jakou superschopnost bych si vybral/a?

Každá otázka má 4 odpovědi. Odpovědi mají být obecné, bezpečné, zábavné a vhodné pro rodinu.

## Business pravidla

- `publicCode` musí být unikátní, krátký a URL-safe.
- Challenge musí mít právě 10 otázek.
- Každá otázka musí mít právě 4 možnosti.
- Tvůrce musí vybrat právě jednu odpověď pro každou otázku.
- Hráč musí odeslat právě jednu odpověď pro každou otázku.
- Jméno tvůrce i hráče musí být povinné a délkově omezené.
- Duplicitní jméno hráče v rámci jedné challenge je povolené.
- Každé odeslání hráče vytvoří samostatný výsledek.
- Správné odpovědi tvůrce se nesmí poslat klientovi před odesláním odpovědí hráče.
- Leaderboard se může zobrazit po odeslání odpovědí.
- Leaderboard má pro MVP stačit jako top 20.
- Odpovědi se skórují deterministicky na serveru.
- Server je autorita pro výpočet skóre i pořadí.

## UX pravidla

- UI musí být v češtině.
- Primární použití je mobil.
- Vytvoření challenge musí působit jako proces na cca 1 minutu.
- Texty musí být krátké a sdílecí.
- Po vytvoření challenge musí být zřejmé, co má uživatel udělat dál: sdílet odkaz.
- Po výsledku musí být CTA `Vytvořit vlastní kvíz` výraznější než sekundární odkazy.
- Nepřidávej do první verze registraci, složité nastavení, veřejný katalog ani administraci.

## Sdílecí texty

Po vytvoření:

```text
Vytvořil/a jsem kvíz „Jak dobře mě znáš?“
Zkus, kolik dáš bodů:
{link}
```

Po dohrání:

```text
Dal/a jsem {score}/10 v kvízu „Jak dobře znáš {creatorName}?“
Překonáš mě?
{link}
```

## Mimo scope pro první verzi

- AI generování otázek.
- Vlastní otázky.
- Textové odpovědi.
- Obrázky, audio, video.
- Registrace a profily.
- Mazání challenge přes UI.
- Moderace výsledků.
- Platební funkce.
- Reklamní systém.
- E-mailové notifikace.
- Sociální login.
- Globální veřejný katalog challenge.
