# Architektura a datový model pro Challenge mód

## Principy

- Challenge mód je samostatný modul v existujícím modulárním monolitu.
- Nesmí přepisovat live Pub kvíz session logiku.
- Nepotřebuje SignalR.
- Server je autorita pro správné odpovědi, skóre a leaderboard.
- Klient nikdy nemá dostat správné odpovědi před odesláním hráčových odpovědí.
- Časy se ukládají v UTC.
- Datový model má být jednoduchý a čitelný.

## Doporučené logické členění

V existujících projektech zachovej aktuální strukturu. Pokud projekt už má jiný styl složek, přizpůsob se mu.

Doporučené názvy složek:

```text
QuizApp.Server/Challenges/
QuizApp.Shared/Challenges/
QuizApp.Client/Pages/Challenge/
```

nebo ekvivalent podle existujícího stylu repozitáře.

## Minimální entity

### Challenge

- `ChallengeId`
- `PublicCode`
- `Title`
- `CreatorName`
- `CreatedAtUtc`
- `IsDeleted`

Volitelné pro budoucí správu:

- `CreatorTokenHash`
- `DeletedAtUtc`

V první verzi není nutné implementovat UI pro správu ani mazání challenge, ale datový model může být připravený.

### ChallengeQuestion

- `ChallengeQuestionId`
- `ChallengeId`
- `OrderIndex`
- `Text`
- `CreatorSelectedOptionKey`

### ChallengeAnswerOption

- `ChallengeAnswerOptionId`
- `ChallengeQuestionId`
- `OptionKey`
- `Text`

### ChallengeSubmission

- `ChallengeSubmissionId`
- `ChallengeId`
- `ParticipantName`
- `Score`
- `MaxScore`
- `SubmittedAtUtc`

### ChallengeSubmissionAnswer

- `ChallengeSubmissionAnswerId`
- `ChallengeSubmissionId`
- `ChallengeQuestionId`
- `SelectedOptionKey`
- `IsCorrect`

## Indexy a omezení

- `Challenge.PublicCode` musí být unikátní.
- `ChallengeQuestion` má unikátní kombinaci `ChallengeId + OrderIndex`.
- `ChallengeAnswerOption` má unikátní kombinaci `ChallengeQuestionId + OptionKey`.
- `ChallengeSubmission` má index na `ChallengeId`.
- `ChallengeSubmissionAnswer` má unikátní kombinaci `ChallengeSubmissionId + ChallengeQuestionId`.

## DTO návrh

### CreateChallengeRequest

- `creatorName`
- `title`
- `answers`
  - `templateQuestionId`
  - `selectedOptionKey`

### CreateChallengeResponse

- `challengeId`
- `publicCode`
- `title`
- `creatorName`

Klient si z `publicCode` sestaví URL podle aktuálního hostu.

### GetChallengeResponse

- `publicCode`
- `title`
- `creatorName`
- `questions`
  - `questionId`
  - `orderIndex`
  - `text`
  - `options`
    - `optionKey`
    - `text`

Nesmí obsahovat správné odpovědi.

### SubmitChallengeAnswersRequest

- `participantName`
- `answers`
  - `questionId`
  - `selectedOptionKey`

### SubmitChallengeAnswersResponse

- `submissionId`
- `score`
- `maxScore`
- `rank`
- `leaderboard`

### ChallengeLeaderboardResponse

- `publicCode`
- `title`
- `creatorName`
- `entries`
  - `rank`
  - `participantName`
  - `score`
  - `maxScore`
  - `submittedAtUtc`

## Šablona otázek

Pevná šablona může být v aplikační službě jako statický seznam nebo v konfigurační třídě. Pro první MVP není nutné ukládat šablonu zvlášť do databáze.

Při vytvoření challenge se otázky a odpovědi zkopírují do tabulek `ChallengeQuestion` a `ChallengeAnswerOption`, aby pozdější změna šablony nerozbila staré challenge.

## PublicCode

Požadavky:
- URL-safe,
- krátký,
- neuhodnutelný přiměřeně pro veřejnou zábavnou funkci,
- unikátní v databázi.

Doporučení:
- 8 až 12 znaků,
- znaky bez snadno zaměnitelných symbolů,
- při kolizi vygenerovat znovu.

## Integrace do existující aplikace

Přidej vstupní odkaz nebo dlaždici pro Challenge mód bez zásahu do existujícího Organizer/Player flow.

Doporučené routy:

```text
/challenge/create
/challenge/{publicCode}
/challenge/{publicCode}/result/{submissionId}
```

API routy jsou popsané v `03-api-signalr-and-security.md`.
