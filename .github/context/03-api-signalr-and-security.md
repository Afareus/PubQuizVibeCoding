# API, SignalR a bezpečnostní zásady

## Organizátorské REST endpointy
- `POST /api/quizzes`
- `POST /api/quizzes/{quizId}/import-csv`
- `GET /api/quizzes/{quizId}`
- `DELETE /api/quizzes/{quizId}`
- `POST /api/quizzes/{quizId}/sessions`
- `GET /api/sessions/{sessionId}`
- `POST /api/sessions/{sessionId}/start`
- `POST /api/sessions/{sessionId}/cancel`
- `GET /api/sessions/{sessionId}/results`
- `GET /api/sessions/{sessionId}/correct-answers`

## Týmové REST endpointy
- `POST /api/sessions/join`
- `GET /api/sessions/{sessionId}/state?teamId={teamId}`
- `POST /api/sessions/{sessionId}/answers`

## Povinné API zásady
- Odpověď lze odeslat pouze jednou.
- Request pro submit musí nést `TeamId` a hlavičku `X-Team-Reconnect-Token`.
- Duplicitní submit vrací chybu.
- Pozdní odpověď vrací chybu.
- Organizátorské endpointy kromě vytvoření kvízu vyžadují `X-Organizer-Token`.
- Stavové endpointy musí vracet serverový snapshot se stavem session a časovými údaji pro lokální countdown.
- Všechny organizátorské session mutace musí respektovat concurrency.

## Doporučený chybový model
- `400` – `ValidationFailed`, `CsvValidationFailed`
- `401` – `MissingAuthToken`
- `403` – `InvalidAuthToken`
- `404` – `ResourceNotFound`
- `409` – `TeamNameAlreadyUsed`, `QuestionClosed`, `AlreadyAnswered`, `SessionStateChanged`, `QuizHasActiveSessions`, `QuizHasNoQuestions`
- `429` – `RateLimited`

## SignalR události
- `session.created`
- `team.joined`
- `session.started`
- `question.changed`
- `session.finished`
- `session.cancelled`
- `results.ready`

## Zásady real-time vrstvy
- Neposílej tick každou sekundu.
- Posílej jen informace potřebné pro lokální countdown.
- Organizátor i týmy se připojují do session-specific skupin.
- Po reconnectu musí jít obnovit aktuální stav přes REST snapshot + SignalR resubscribe.

## Bezpečnostní zásady
- `QuizOrganizerToken` generuj s minimálně 256bit entropií.
- Zobraz ho pouze jednou při vytvoření kvízu.
- Na serveru ukládej pouze hash.
- Porovnání tokenů prováděj constant-time.
- Administrátorké heslo kvízu ukládej pouze jako hash.
- Chraň local storage designem UI a minimalizací XSS rizika.
- Mimo localhost vyžaduj HTTPS/TLS.

## Rate limiting minimum
- join: minimálně 10 pokusů/min/IP/session
- submit: minimálně 20 requestů/min/team
- organizátorské mutace: minimálně 10 requestů/min/token

## Auditované akce
- `QUIZ_CREATED`
- `QUIZ_IMPORTED`
- `SESSION_CREATED`
- `SESSION_STARTED`
- `SESSION_CANCELLED`
- `QUIZ_DELETED`
