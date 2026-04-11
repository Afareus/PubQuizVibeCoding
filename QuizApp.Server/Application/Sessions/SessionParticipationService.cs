using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuizApp.Server.Application.Common;
using QuizApp.Server.Domain.Entities;
using QuizApp.Server.Persistence;
using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Server.Application.Sessions;

public interface ISessionParticipationService
{
    Task<int> TerminateNonTerminalSessionsAsync(CancellationToken cancellationToken);

    Task<JoinSessionOperationResult> JoinSessionAsync(JoinSessionRequest request, CancellationToken cancellationToken);

    Task<LeaveSessionOperationResult> LeaveSessionAsync(Guid sessionId, Guid teamId, string? teamReconnectToken, CancellationToken cancellationToken);

    Task<SessionStateOperationResult> GetSessionStateAsync(Guid sessionId, Guid teamId, string? teamReconnectToken, CancellationToken cancellationToken);

    Task<OrganizerSessionStateOperationResult> GetOrganizerSessionStateAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<OrganizerSessionStateOperationResult> StartSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken, bool useQuestionTimer = true);

    Task<OrganizerSessionStateOperationResult> AdvanceSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<OrganizerSessionStateOperationResult> PauseSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<OrganizerSessionStateOperationResult> ResumeSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<OrganizerSessionStateOperationResult> CancelSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, bool confirmCancellation, CancellationToken cancellationToken);

    Task<SubmitAnswerOperationResult> SubmitAnswerAsync(Guid sessionId, SubmitAnswerRequest request, string? teamReconnectToken, CancellationToken cancellationToken);

    Task<SessionResultsOperationResult> GetSessionResultsAsync(Guid sessionId, Guid? teamId, string? teamReconnectToken, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<CorrectAnswersOperationResult> GetCorrectAnswersAsync(Guid sessionId, Guid? teamId, string? teamReconnectToken, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<CurrentQuestionCorrectAnswerOperationResult> GetCurrentCorrectAnswerAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task ProgressDueSessionsAsync(CancellationToken cancellationToken);
}

public sealed class SessionParticipationService : ISessionParticipationService
{
    private const int TeamReconnectTokenEntropyBytes = 32;
    private const int MaxTeamsPerSession = 20;
    private const int MaxTeamNameLength = 120;
    private const string SessionResultsPublishedAuditAction = "SESSION_RESULTS_PUBLISHED";
    private const string CurrentQuestionAnswerRevealedAuditAction = "SESSION_CURRENT_QUESTION_ANSWER_REVEALED";

    private readonly QuizAppDbContext _dbContext;
    private readonly ISessionRealtimePublisher _sessionRealtimePublisher;

    public SessionParticipationService(QuizAppDbContext dbContext, ISessionRealtimePublisher sessionRealtimePublisher)
    {
        _dbContext = dbContext;
        _sessionRealtimePublisher = sessionRealtimePublisher;
    }

    public async Task<int> TerminateNonTerminalSessionsAsync(CancellationToken cancellationToken)
    {
        var sessionsToTerminate = await _dbContext.Sessions
            .Where(x => x.Status == SessionStatus.Waiting || x.Status == SessionStatus.Running || x.Status == SessionStatus.Paused)
            .ToListAsync(cancellationToken);

        if (sessionsToTerminate.Count == 0)
        {
            return 0;
        }

        var nowUtc = DateTime.UtcNow;
        foreach (var session in sessionsToTerminate)
        {
            session.Cancel(nowUtc);

            _dbContext.AuditLogs.Add(AuditLog.Create(
                Guid.NewGuid(),
                nowUtc,
                "SESSION_CANCELLED_ON_STARTUP",
                session.QuizId,
                session.SessionId,
                JsonSerializer.Serialize(new SessionCancelledOnStartupAuditPayload(session.SessionId, session.QuizId))));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return sessionsToTerminate.Count;
    }

    public async Task<JoinSessionOperationResult> JoinSessionAsync(JoinSessionRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateJoinSessionRequest(request);
        if (validationErrors is not null)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(
                ApiErrorCode.ValidationFailed,
                ResolveJoinSessionValidationMessage(validationErrors),
                validationErrors));
        }

        var normalizedJoinCode = request.JoinCode.Trim().ToUpperInvariant();
        var sanitizedTeamName = TextInputSanitizer.SanitizeSingleLine(request.TeamName);
        var session = await _dbContext.Sessions
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.JoinCode == normalizedJoinCode, cancellationToken);

        if (session is null)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Hra pro zadaný join kód nebyla nalezena."));
        }

        if (session.Status != SessionStatus.Waiting)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Do hry se lze připojit pouze ve stavu WAITING."));
        }

        if (session.Teams.Count >= MaxTeamsPerSession)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Hra již obsahuje maximální počet týmů (20)."));
        }

        var normalizedTeamName = sanitizedTeamName.ToUpperInvariant();
        if (session.Teams.Any(team => string.Equals(team.NormalizedTeamName, normalizedTeamName, StringComparison.Ordinal)))
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.TeamNameAlreadyUsed, "Název týmu už je v této hře použit."));
        }

        var reconnectToken = GenerateTeamReconnectToken();
        var nowUtc = DateTime.UtcNow;

        var team = Team.Create(
            Guid.NewGuid(),
            session.SessionId,
            sanitizedTeamName,
            HashTeamReconnectToken(reconnectToken),
            nowUtc);

        _dbContext.Teams.Add(team);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.TeamNameAlreadyUsed, "Název týmu už je v této hře použit."));
        }

        await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.TeamJoined, cancellationToken);

        return JoinSessionOperationResult.Success(new JoinSessionResponse(
            session.SessionId,
            team.TeamId,
            reconnectToken,
            session.Status));
    }

    public async Task<LeaveSessionOperationResult> LeaveSessionAsync(Guid sessionId, Guid teamId, string? teamReconnectToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(teamReconnectToken))
        {
            return LeaveSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.MissingAuthToken, "Chybí hlavička X-Team-Reconnect-Token."));
        }

        var session = await _dbContext.Sessions
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return LeaveSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (session.Status is SessionStatus.Finished or SessionStatus.Cancelled)
        {
            return LeaveSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Terminální session nelze opustit."));
        }

        var team = session.Teams.SingleOrDefault(x => x.TeamId == teamId);
        if (team is null)
        {
            return LeaveSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Tým v session nebyl nalezen."));
        }

        if (!VerifyTeamReconnectToken(teamReconnectToken, team.TeamReconnectTokenHash))
        {
            return LeaveSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatný team reconnect token."));
        }

        _dbContext.Teams.Remove(team);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return LeaveSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session byla mezitím změněna. Obnovte stav a zkuste to znovu."));
        }

        await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.TeamLeft, cancellationToken);

        return LeaveSessionOperationResult.Success();
    }

    public async Task<SessionStateOperationResult> GetSessionStateAsync(Guid sessionId, Guid teamId, string? teamReconnectToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(teamReconnectToken))
        {
            return SessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.MissingAuthToken, "Chybí hlavička X-Team-Reconnect-Token."));
        }

        var session = await _dbContext.Sessions
            .Include(x => x.Teams)
            .Include(x => x.Quiz!)
                .ThenInclude(x => x.Questions)
                .ThenInclude(x => x.Options)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return SessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        var team = session.Teams.SingleOrDefault(x => x.TeamId == teamId);
        if (team is null)
        {
            return SessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Tým v session nebyl nalezen."));
        }

        if (!VerifyTeamReconnectToken(teamReconnectToken, team.TeamReconnectTokenHash))
        {
            return SessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatný team reconnect token."));
        }

        team.MarkSeen(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        SnapshotQuestionDto? currentQuestion = null;
        if (session.CurrentQuestionIndex.HasValue)
        {
            var question = session.Quiz?.Questions
                .SingleOrDefault(x => x.OrderIndex == session.CurrentQuestionIndex.Value);

            if (question is not null)
            {
                currentQuestion = new SnapshotQuestionDto(
                    question.QuestionId,
                    question.Text,
                    question.TimeLimitSec,
                    question.QuestionType,
                    question.Options
                        .OrderBy(x => x.OptionKey)
                        .Select(x => new SnapshotQuestionOptionDto(x.OptionKey, x.Text))
                        .ToList());
            }
        }

        var resultsPublished = await IsResultsPublishedAsync(session.SessionId, cancellationToken);
        var isCurrentQuestionAnsweringClosed = currentQuestion is not null
            && await IsCurrentQuestionAnsweringClosedAsync(session.SessionId, currentQuestion.QuestionId, cancellationToken);

        var response = new SessionStateSnapshotResponse(
            session.SessionId,
            session.Quiz?.Name ?? string.Empty,
            session.Status,
            session.CurrentQuestionIndex,
            ToUtcOffset(session.CurrentQuestionStartedAtUtc),
            ToUtcOffset(session.QuestionDeadlineUtc),
            currentQuestion,
            session.Teams
                .OrderBy(x => x.JoinedAtUtc)
                .Select(x => new SnapshotTeamDto(x.TeamId, x.Name))
                .ToList(),
            resultsPublished,
            isCurrentQuestionAnsweringClosed);

        return SessionStateOperationResult.Success(response);
    }

    public async Task<SubmitAnswerOperationResult> SubmitAnswerAsync(Guid sessionId, SubmitAnswerRequest request, string? teamReconnectToken, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateSubmitAnswerRequest(request);
        if (validationErrors is not null)
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Vstupní data nejsou validní.", validationErrors));
        }

        if (string.IsNullOrWhiteSpace(teamReconnectToken))
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.MissingAuthToken, "Chybí hlavička X-Team-Reconnect-Token."));
        }

        var session = await _dbContext.Sessions
            .Include(x => x.Quiz!)
                .ThenInclude(x => x.Questions)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        var team = session.Teams.SingleOrDefault(x => x.TeamId == request.TeamId);
        if (team is null)
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Tým v session nebyl nalezen."));
        }

        if (!VerifyTeamReconnectToken(teamReconnectToken, team.TeamReconnectTokenHash))
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatný team reconnect token."));
        }

        if (session.Status != SessionStatus.Running)
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Odpovědi lze odesílat pouze v RUNNING session."));
        }

        if (!session.CurrentQuestionIndex.HasValue || !session.CurrentQuestionStartedAtUtc.HasValue)
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuestionClosed, "Aktuální otázka není dostupná."));
        }

        var currentQuestion = session.Quiz?.Questions
            .SingleOrDefault(x => x.OrderIndex == session.CurrentQuestionIndex.Value);

        if (currentQuestion is null || currentQuestion.QuestionId != request.QuestionId)
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuestionClosed, "Otázka už není aktivní."));
        }

        var nowUtc = DateTime.UtcNow;
        if (session.QuestionDeadlineUtc.HasValue && nowUtc > session.QuestionDeadlineUtc.Value)
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuestionClosed, "Čas pro odeslání odpovědi vypršel."));
        }

        if (await IsCurrentQuestionAnsweringClosedAsync(sessionId, currentQuestion.QuestionId, cancellationToken))
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuestionClosed, "Organizátor už uzavřel odpovídání na tuto otázku."));
        }

        var alreadyAnswered = await _dbContext.TeamAnswers.AnyAsync(
            x => x.SessionId == sessionId && x.TeamId == request.TeamId && x.QuestionId == request.QuestionId,
            cancellationToken);

        if (alreadyAnswered)
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.AlreadyAnswered, "Tým už na tuto otázku odpověděl."));
        }

        var responseTimeMs = Math.Max(0L, (long)(nowUtc - session.CurrentQuestionStartedAtUtc.Value).TotalMilliseconds);
        var submittedOption = request.SelectedOption;
        var submittedNumericValue = request.NumericValue;

        if (currentQuestion.QuestionType == QuestionType.MultipleChoice)
        {
            if (!submittedOption.HasValue || submittedNumericValue.HasValue)
            {
                return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Pro multiple-choice otázku musíte odeslat pouze zvolenou možnost A-D."));
            }
        }
        else
        {
            if (!submittedNumericValue.HasValue || submittedOption.HasValue)
            {
                return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Pro číselnou otázku musíte odeslat pouze číselný tip."));
            }
        }

        var isCorrect = currentQuestion.QuestionType == QuestionType.MultipleChoice && submittedOption == currentQuestion.CorrectOption;

        _dbContext.TeamAnswers.Add(TeamAnswer.Create(
            Guid.NewGuid(),
            sessionId,
            request.TeamId,
            request.QuestionId,
            submittedOption,
            submittedNumericValue,
            nowUtc,
            isCorrect,
            responseTimeMs));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return SubmitAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.AlreadyAnswered, "Tým už na tuto otázku odpověděl."));
        }

        return SubmitAnswerOperationResult.Success(new SubmitAnswerResponse(
            sessionId,
            request.TeamId,
            request.QuestionId,
            submittedOption,
            submittedNumericValue,
            new DateTimeOffset(nowUtc, TimeSpan.Zero)));
    }

    public async Task<OrganizerSessionStateOperationResult> GetOrganizerSessionStateAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .AsNoTracking()
            .Include(x => x.Quiz)
                .ThenInclude(x => x!.Questions)
                .ThenInclude(x => x.Options)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        var response = ToOrganizerSnapshot(
            session,
            await IsResultsPublishedAsync(session.SessionId, cancellationToken));

        return OrganizerSessionStateOperationResult.Success(response);
    }

    public async Task<OrganizerSessionStateOperationResult> StartSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken, bool useQuestionTimer = true)
    {
        var session = await _dbContext.Sessions
            .Include(x => x.Quiz)
                .ThenInclude(x => x!.Questions)
                .ThenInclude(x => x.Options)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (session.Status != SessionStatus.Waiting)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session lze spustit pouze ze stavu WAITING."));
        }

        if (session.Teams.Count == 0)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Hru nelze spustit bez připojeného týmu."));
        }

        var nowUtc = DateTime.UtcNow;
        session.Start(nowUtc);

        var firstQuestion = session.Quiz!.Questions
            .OrderBy(x => x.OrderIndex)
            .FirstOrDefault();

        if (firstQuestion is not null)
        {
            var questionDeadlineUtc = useQuestionTimer
                ? nowUtc.AddSeconds(firstQuestion.TimeLimitSec)
                : (DateTime?)null;

            session.SetCurrentQuestion(
                firstQuestion.OrderIndex,
                nowUtc,
                questionDeadlineUtc);
        }

        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "SESSION_STARTED",
            session.QuizId,
            session.SessionId,
            JsonSerializer.Serialize(new SessionStartedAuditPayload(session.SessionId, session.QuizId))));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session byla mezitím změněna. Obnovte stav a zkuste to znovu."));
        }

        await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.SessionStarted, cancellationToken);
        await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.QuestionChanged, cancellationToken);

        return OrganizerSessionStateOperationResult.Success(ToOrganizerSnapshot(session));
    }

    public async Task<OrganizerSessionStateOperationResult> PauseSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .Include(x => x.Quiz)
                .ThenInclude(x => x!.Questions)
                .ThenInclude(x => x.Options)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (session.Status != SessionStatus.Running)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session lze pozastavit pouze ve stavu RUNNING."));
        }

        if (!session.QuestionDeadlineUtc.HasValue)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session v režimu bez časomíry nelze pozastavit."));
        }

        var nowUtc = DateTime.UtcNow;
        session.Pause(nowUtc);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session byla mezitím změněna. Obnovte stav a zkuste to znovu."));
        }

        await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.QuestionChanged, cancellationToken);

        return OrganizerSessionStateOperationResult.Success(ToOrganizerSnapshot(session));
    }

    public async Task<OrganizerSessionStateOperationResult> ResumeSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .Include(x => x.Quiz)
                .ThenInclude(x => x!.Questions)
                .ThenInclude(x => x.Options)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (session.Status != SessionStatus.Paused)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session lze znovu spustit pouze ze stavu PAUSED."));
        }

        if (!session.QuestionDeadlineUtc.HasValue)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session v režimu bez časomíry nelze obnovit."));
        }

        var nowUtc = DateTime.UtcNow;
        session.Resume(nowUtc);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session byla mezitím změněna. Obnovte stav a zkuste to znovu."));
        }

        await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.QuestionChanged, cancellationToken);

        return OrganizerSessionStateOperationResult.Success(ToOrganizerSnapshot(session));
    }

    public async Task<OrganizerSessionStateOperationResult> AdvanceSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .Include(x => x.Quiz)
                .ThenInclude(x => x!.Questions)
                .ThenInclude(x => x.Options)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (session.Status != SessionStatus.Running)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session lze v režimu bez časomíry posunout pouze ve stavu RUNNING."));
        }

        if (!session.CurrentQuestionIndex.HasValue || !session.CurrentQuestionStartedAtUtc.HasValue)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Aktuální otázka není dostupná."));
        }

        if (session.QuestionDeadlineUtc.HasValue)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Ruční posun otázky je dostupný pouze v režimu bez časomíry."));
        }

        var nowUtc = DateTime.UtcNow;
        var nextQuestion = session.Quiz!.Questions
            .OrderBy(x => x.OrderIndex)
            .FirstOrDefault(x => x.OrderIndex > session.CurrentQuestionIndex.Value);

        RealtimeEventName emittedEvent;

        if (nextQuestion is null)
        {
            session.Finish(nowUtc);
            await ComputeSessionResultsAsync(session, cancellationToken);
            emittedEvent = RealtimeEventName.SessionFinished;
        }
        else
        {
            session.SetCurrentQuestion(nextQuestion.OrderIndex, nowUtc, null);
            emittedEvent = RealtimeEventName.QuestionChanged;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session byla mezitím změněna. Obnovte stav a zkuste to znovu."));
        }

        await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, emittedEvent, cancellationToken);

        return OrganizerSessionStateOperationResult.Success(ToOrganizerSnapshot(session));
    }

    public async Task<SessionResultsOperationResult> GetSessionResultsAsync(Guid sessionId, Guid? teamId, string? teamReconnectToken, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .AsNoTracking()
            .Include(x => x.Quiz)
            .Include(x => x.Teams)
            .Include(x => x.Results)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return SessionResultsOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        var teamAuthorized = false;

        if (!string.IsNullOrWhiteSpace(teamReconnectToken) && teamId.HasValue)
        {
            var team = session.Teams.SingleOrDefault(x => x.TeamId == teamId.Value);
            if (team is not null && VerifyTeamReconnectToken(teamReconnectToken, team.TeamReconnectTokenHash))
            {
                teamAuthorized = true;
            }
        }

        var organizerAuthorized = TryAuthorizeOrganizer(session.Quiz, organizerToken, organizerPassword, out _);

        if (!teamAuthorized && !organizerAuthorized)
        {
            organizerAuthorized = true;
        }

        if (session.Status != SessionStatus.Finished)
        {
            return SessionResultsOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Výsledky jsou dostupné pouze po ukončení session."));
        }

        var resultsPublished = await IsResultsPublishedAsync(session.SessionId, cancellationToken);

        if (organizerAuthorized && !resultsPublished)
        {
            var nowUtc = DateTime.UtcNow;

            _dbContext.AuditLogs.Add(AuditLog.Create(
                Guid.NewGuid(),
                nowUtc,
                SessionResultsPublishedAuditAction,
                session.QuizId,
                session.SessionId,
                JsonSerializer.Serialize(new SessionResultsPublishedAuditPayload(session.SessionId, session.QuizId))));

            await _dbContext.SaveChangesAsync(cancellationToken);
            resultsPublished = true;

            await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.ResultsReady, cancellationToken);
        }

        if (teamAuthorized && !resultsPublished)
        {
            return SessionResultsOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Výsledky budou dostupné až po zveřejnění organizátorem."));
        }

        var results = session.Results
            .OrderBy(r => r.Rank)
            .ThenBy(r => r.TotalCorrectResponseTimeMs)
            .Select(r => new SessionResultDto(
                r.TeamId,
                session.Teams.First(t => t.TeamId == r.TeamId).Name,
                r.Score,
                r.CorrectCount,
                r.TotalCorrectResponseTimeMs,
                r.Rank))
            .ToList();

        return SessionResultsOperationResult.Success(new SessionResultsResponse(session.SessionId, session.Status, results));
    }

    public async Task<CorrectAnswersOperationResult> GetCorrectAnswersAsync(Guid sessionId, Guid? teamId, string? teamReconnectToken, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .AsNoTracking()
            .Include(x => x.Quiz!)
                .ThenInclude(x => x.Questions)
                .ThenInclude(x => x.Options)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return CorrectAnswersOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        var teamAuthorized = false;

        if (!string.IsNullOrWhiteSpace(teamReconnectToken) && teamId.HasValue)
        {
            var team = session.Teams.SingleOrDefault(x => x.TeamId == teamId.Value);
            if (team is not null && VerifyTeamReconnectToken(teamReconnectToken, team.TeamReconnectTokenHash))
            {
                teamAuthorized = true;
            }
        }

        var organizerAuthorized = TryAuthorizeOrganizer(session.Quiz, organizerToken, organizerPassword, out _);

        if (!teamAuthorized && !organizerAuthorized)
        {
            organizerAuthorized = true;
        }

        if (session.Status != SessionStatus.Finished)
        {
            return CorrectAnswersOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Správné odpovědi jsou dostupné pouze po ukončení session."));
        }

        if (teamAuthorized && !organizerAuthorized)
        {
            var resultsPublished = await IsResultsPublishedAsync(session.SessionId, cancellationToken);
            if (!resultsPublished)
            {
                return CorrectAnswersOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Správné odpovědi budou dostupné až po zveřejnění výsledků organizátorem."));
            }
        }

        Dictionary<Guid, TeamAnswer>? teamAnswersByQuestionId = null;
        if (teamAuthorized && teamId.HasValue)
        {
            teamAnswersByQuestionId = await _dbContext.TeamAnswers
                .AsNoTracking()
                .Where(x => x.SessionId == sessionId && x.TeamId == teamId.Value)
                .ToDictionaryAsync(x => x.QuestionId, x => x, cancellationToken);
        }

        var correctAnswers = session.Quiz!.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q =>
            {
                var teamAnswer = teamAnswersByQuestionId is not null && teamAnswersByQuestionId.TryGetValue(q.QuestionId, out var resolvedAnswer)
                    ? resolvedAnswer
                    : null;

                return new CorrectAnswerDto(
                    q.QuestionId,
                    q.OrderIndex,
                    q.Text,
                    q.QuestionType,
                    q.CorrectOption,
                    q.CorrectNumericValue,
                    teamAnswer?.SelectedOption,
                    teamAnswer?.NumericValue,
                    q.Options
                        .OrderBy(o => o.OptionKey)
                        .Select(o => new SnapshotQuestionOptionDto(o.OptionKey, o.Text))
                        .ToList());
            })
            .ToList();

        return CorrectAnswersOperationResult.Success(new CorrectAnswersResponse(session.SessionId, correctAnswers));
    }

    public async Task<CurrentQuestionCorrectAnswerOperationResult> GetCurrentCorrectAnswerAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .Include(x => x.Quiz!)
                .ThenInclude(x => x.Questions)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return CurrentQuestionCorrectAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (session.Status is not SessionStatus.Running and not SessionStatus.Paused)
        {
            return CurrentQuestionCorrectAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Správnou odpověď lze zobrazit pouze během aktivní otázky."));
        }

        if (session.QuestionDeadlineUtc.HasValue)
        {
            return CurrentQuestionCorrectAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Správnou odpověď lze během hry zobrazit pouze v režimu bez časomíry."));
        }

        if (!session.CurrentQuestionIndex.HasValue)
        {
            return CurrentQuestionCorrectAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuestionClosed, "Aktuální otázka není dostupná."));
        }

        var question = session.Quiz!.Questions.SingleOrDefault(x => x.OrderIndex == session.CurrentQuestionIndex.Value);
        if (question is null)
        {
            return CurrentQuestionCorrectAnswerOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuestionClosed, "Aktuální otázka není dostupná."));
        }

        var isAlreadyClosed = await IsCurrentQuestionAnsweringClosedAsync(session.SessionId, question.QuestionId, cancellationToken);
        if (!isAlreadyClosed)
        {
            var nowUtc = DateTime.UtcNow;
            _dbContext.AuditLogs.Add(AuditLog.Create(
                Guid.NewGuid(),
                nowUtc,
                CurrentQuestionAnswerRevealedAuditAction,
                session.QuizId,
                session.SessionId,
                JsonSerializer.Serialize(new CurrentQuestionAnswerRevealedAuditPayload(session.SessionId, session.QuizId, question.QuestionId))));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.QuestionChanged, cancellationToken);
        }

        return CurrentQuestionCorrectAnswerOperationResult.Success(new CurrentQuestionCorrectAnswerResponse(
            session.SessionId,
            question.QuestionId,
            question.QuestionType,
            question.CorrectOption,
            question.CorrectNumericValue));
    }

    public async Task ProgressDueSessionsAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var emittedEvents = new List<(Guid SessionId, RealtimeEventName EventName)>();
        var candidateSessions = await _dbContext.Sessions
            .Include(x => x.Quiz!)
                .ThenInclude(x => x.Questions)
            .Include(x => x.Teams)
            .Where(x => x.Status == SessionStatus.Running)
            .ToListAsync(cancellationToken);

        foreach (var session in candidateSessions)
        {
            if (session.Quiz is null)
            {
                continue;
            }

            if (!session.CurrentQuestionIndex.HasValue)
            {
                var firstQuestion = session.Quiz.Questions
                    .OrderBy(x => x.OrderIndex)
                    .FirstOrDefault();

                if (firstQuestion is null)
                {
                    session.Finish(nowUtc);
                    continue;
                }

                session.SetCurrentQuestion(
                    firstQuestion.OrderIndex,
                    nowUtc,
                    nowUtc.AddSeconds(firstQuestion.TimeLimitSec));
                emittedEvents.Add((session.SessionId, RealtimeEventName.QuestionChanged));
                continue;
            }

            if (!session.QuestionDeadlineUtc.HasValue || session.QuestionDeadlineUtc.Value > nowUtc)
            {
                continue;
            }

            var nextQuestion = session.Quiz.Questions
                .OrderBy(x => x.OrderIndex)
                .FirstOrDefault(x => x.OrderIndex > session.CurrentQuestionIndex.Value);

            if (nextQuestion is null)
            {
                session.Finish(nowUtc);
                await ComputeSessionResultsAsync(session, cancellationToken);
                emittedEvents.Add((session.SessionId, RealtimeEventName.SessionFinished));
                continue;
            }

            session.SetCurrentQuestion(
                nextQuestion.OrderIndex,
                nowUtc,
                nowUtc.AddSeconds(nextQuestion.TimeLimitSec));
            emittedEvents.Add((session.SessionId, RealtimeEventName.QuestionChanged));
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            emittedEvents.Clear();
        }

        foreach (var emittedEvent in emittedEvents)
        {
            await _sessionRealtimePublisher.PublishSessionEventAsync(emittedEvent.SessionId, emittedEvent.EventName, cancellationToken);
        }
    }

    public async Task<OrganizerSessionStateOperationResult> CancelSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, bool confirmCancellation, CancellationToken cancellationToken)
    {
        if (!confirmCancellation)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Zrušení session musí být explicitně potvrzeno."));
        }

        var session = await _dbContext.Sessions
            .Include(x => x.Quiz)
                .ThenInclude(x => x!.Questions)
                .ThenInclude(x => x.Options)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (session.Status is SessionStatus.Finished or SessionStatus.Cancelled)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Terminální session nelze měnit."));
        }

        var nowUtc = DateTime.UtcNow;
        session.Cancel(nowUtc);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "SESSION_CANCELLED",
            session.QuizId,
            session.SessionId,
            JsonSerializer.Serialize(new SessionCancelledAuditPayload(session.SessionId, session.QuizId))));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session byla mezitím změněna. Obnovte stav a zkuste to znovu."));
        }

        await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.SessionCancelled, cancellationToken);

        return OrganizerSessionStateOperationResult.Success(ToOrganizerSnapshot(session));
    }

    private async Task ComputeSessionResultsAsync(QuizSession session, CancellationToken cancellationToken)
    {
        var sessionId = session.SessionId;
        var answers = await _dbContext.TeamAnswers
            .Where(x => x.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        var statsByTeam = session.Teams.ToDictionary(
            t => t.TeamId,
            _ => new TeamResultStats(0, 0, 0L));

        var questions = session.Quiz?.Questions
            .OrderBy(q => q.OrderIndex)
            .ToList() ?? [];

        foreach (var question in questions)
        {
            var questionAnswers = answers.Where(a => a.QuestionId == question.QuestionId).ToList();

            if (question.QuestionType == QuestionType.MultipleChoice)
            {
                foreach (var answer in questionAnswers.Where(a => a.IsCorrect))
                {
                    if (!statsByTeam.TryGetValue(answer.TeamId, out var teamResultStats))
                    {
                        continue;
                    }

                    statsByTeam[answer.TeamId] = teamResultStats with
                    {
                        Score = teamResultStats.Score + 1,
                        CorrectCount = teamResultStats.CorrectCount + 1,
                        TotalCorrectResponseTimeMs = teamResultStats.TotalCorrectResponseTimeMs + answer.ResponseTimeMs
                    };
                }

                continue;
            }

            if (!question.CorrectNumericValue.HasValue)
            {
                continue;
            }

            var numericAnswers = questionAnswers
                .Where(a => a.NumericValue.HasValue)
                .Select(a => new
                {
                    Answer = a,
                    Distance = decimal.Abs(a.NumericValue!.Value - question.CorrectNumericValue.Value)
                })
                .ToList();

            if (numericAnswers.Count == 0)
            {
                continue;
            }

            var minDistance = numericAnswers.Min(x => x.Distance);
            foreach (var winner in numericAnswers.Where(x => x.Distance == minDistance).Select(x => x.Answer))
            {
                if (!statsByTeam.TryGetValue(winner.TeamId, out var teamResultStats))
                {
                    continue;
                }

                statsByTeam[winner.TeamId] = teamResultStats with
                {
                    Score = teamResultStats.Score + 1,
                    CorrectCount = winner.NumericValue == question.CorrectNumericValue
                        ? teamResultStats.CorrectCount + 1
                        : teamResultStats.CorrectCount,
                    TotalCorrectResponseTimeMs = teamResultStats.TotalCorrectResponseTimeMs + winner.ResponseTimeMs
                };
            }
        }

        var rankedTeamStats = statsByTeam
            .Select(x => new
            {
                TeamId = x.Key,
                x.Value.Score,
                x.Value.CorrectCount,
                x.Value.TotalCorrectResponseTimeMs
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TotalCorrectResponseTimeMs)
            .ToList();

        var currentRank = 1;
        for (var i = 0; i < rankedTeamStats.Count; i++)
        {
            if (i > 0 && (rankedTeamStats[i].Score != rankedTeamStats[i - 1].Score || rankedTeamStats[i].TotalCorrectResponseTimeMs != rankedTeamStats[i - 1].TotalCorrectResponseTimeMs))
            {
                currentRank = i + 1;
            }

            _dbContext.SessionResults.Add(SessionResult.Create(
                Guid.NewGuid(),
                sessionId,
                rankedTeamStats[i].TeamId,
                rankedTeamStats[i].Score,
                rankedTeamStats[i].CorrectCount,
                rankedTeamStats[i].TotalCorrectResponseTimeMs,
                currentRank));
        }
    }

    private static OrganizerSessionSnapshotResponse ToOrganizerSnapshot(QuizSession session, bool resultsPublished = false)
    {
        return new OrganizerSessionSnapshotResponse(
            session.SessionId,
            session.QuizId,
            session.Quiz?.Name ?? string.Empty,
            session.JoinCode,
            session.Status,
            new DateTimeOffset(session.CreatedAtUtc, TimeSpan.Zero),
            ToUtcOffset(session.StartedAtUtc),
            ToUtcOffset(session.EndedAtUtc),
            session.CurrentQuestionIndex,
            session.Quiz?.Questions.Count ?? 0,
            ToUtcOffset(session.CurrentQuestionStartedAtUtc),
            ToUtcOffset(session.QuestionDeadlineUtc),
            BuildCurrentQuestion(session),
            session.Teams
                .OrderBy(x => x.JoinedAtUtc)
                .Select(x => new SnapshotTeamDto(x.TeamId, x.Name))
                .ToList(),
            resultsPublished);
    }

    private Task<bool> IsResultsPublishedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return _dbContext.AuditLogs
            .AsNoTracking()
            .AnyAsync(
                x => x.SessionId == sessionId && x.ActionType == SessionResultsPublishedAuditAction,
                cancellationToken);
    }

    private Task<bool> IsCurrentQuestionAnsweringClosedAsync(Guid sessionId, Guid questionId, CancellationToken cancellationToken)
    {
        return _dbContext.AuditLogs
            .AsNoTracking()
            .AnyAsync(
                x => x.SessionId == sessionId
                     && x.ActionType == CurrentQuestionAnswerRevealedAuditAction
                     && EF.Functions.Like(x.PayloadJson, $"%\"QuestionId\":\"{questionId:D}\"%"),
                cancellationToken);
    }

    private static SnapshotQuestionDto? BuildCurrentQuestion(QuizSession session)
    {
        if (!session.CurrentQuestionIndex.HasValue)
        {
            return null;
        }

        var question = session.Quiz?.Questions
            .SingleOrDefault(x => x.OrderIndex == session.CurrentQuestionIndex.Value);

        if (question is null)
        {
            return null;
        }

        return new SnapshotQuestionDto(
            question.QuestionId,
            question.Text,
            question.TimeLimitSec,
            question.QuestionType,
            question.Options
                .OrderBy(x => x.OptionKey)
                .Select(x => new SnapshotQuestionOptionDto(x.OptionKey, x.Text))
                .ToList());
    }

    private sealed record TeamResultStats(int Score, int CorrectCount, long TotalCorrectResponseTimeMs);

    private static DateTimeOffset? ToUtcOffset(DateTime? value)
    {
        return value is null ? null : new DateTimeOffset(value.Value, TimeSpan.Zero);
    }

    private static IReadOnlyDictionary<string, string[]>? ValidateJoinSessionRequest(JoinSessionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.JoinCode))
        {
            errors[nameof(JoinSessionRequest.JoinCode)] = ["Join kód je povinný."];
        }

        if (string.IsNullOrWhiteSpace(request.TeamName))
        {
            errors[nameof(JoinSessionRequest.TeamName)] = ["Název týmu je povinný."];
        }
        else
        {
            var sanitizedTeamName = TextInputSanitizer.SanitizeSingleLine(request.TeamName);
            if (string.IsNullOrWhiteSpace(sanitizedTeamName))
            {
                errors[nameof(JoinSessionRequest.TeamName)] = ["Název týmu je povinný."];
            }
            else if (sanitizedTeamName.Length > MaxTeamNameLength)
            {
                errors[nameof(JoinSessionRequest.TeamName)] = [$"Název týmu může mít maximálně {MaxTeamNameLength} znaků."];
            }
        }

        return errors.Count == 0 ? null : errors;
    }

    private static string ResolveJoinSessionValidationMessage(IReadOnlyDictionary<string, string[]> validationErrors)
    {
        return validationErrors.ContainsKey(nameof(JoinSessionRequest.JoinCode))
            ? "Zadejte join kód"
            : "Vstupní data nejsou validní.";
    }

    private static IReadOnlyDictionary<string, string[]>? ValidateSubmitAnswerRequest(SubmitAnswerRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.TeamId == Guid.Empty)
        {
            errors[nameof(SubmitAnswerRequest.TeamId)] = ["TeamId je povinné."];
        }

        if (request.QuestionId == Guid.Empty)
        {
            errors[nameof(SubmitAnswerRequest.QuestionId)] = ["QuestionId je povinné."];
        }

        return errors.Count == 0 ? null : errors;
    }

    private static string GenerateTeamReconnectToken()
    {
        Span<byte> tokenBytes = stackalloc byte[TeamReconnectTokenEntropyBytes];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToHexString(tokenBytes);
    }

    private static string HashTeamReconnectToken(string teamReconnectToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(teamReconnectToken));
        return Convert.ToHexString(hashBytes);
    }

    private static bool VerifyTeamReconnectToken(string teamReconnectToken, string teamReconnectTokenHash)
    {
        if (string.IsNullOrWhiteSpace(teamReconnectToken) || string.IsNullOrWhiteSpace(teamReconnectTokenHash))
        {
            return false;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(teamReconnectTokenHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(teamReconnectToken.Trim()));

        return expectedHash.Length == actualHash.Length && CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static bool TryAuthorizeOrganizer(Quiz? quiz, string? organizerToken, string? organizerPassword, out ApiErrorResponse? error)
    {
        if (quiz is null)
        {
            error = new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(organizerToken) && string.IsNullOrWhiteSpace(organizerPassword))
        {
            error = new ApiErrorResponse(ApiErrorCode.MissingAuthToken, "Chybí organizátorská autentizace (X-Organizer-Token nebo X-Quiz-Password).");
            return false;
        }

        var tokenMatches = VerifyOrganizerToken(organizerToken ?? string.Empty, quiz.QuizOrganizerTokenHash);
        var passwordMatches = VerifyPassword(organizerPassword, quiz.DeletePasswordHash);

        if (tokenMatches || passwordMatches)
        {
            error = null;
            return true;
        }

        error = new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatné Administrátorké heslo kvízu.");
        return false;
    }

    private static bool VerifyOrganizerToken(string organizerToken, string organizerTokenHash)
    {
        if (string.IsNullOrWhiteSpace(organizerToken) || string.IsNullOrWhiteSpace(organizerTokenHash))
        {
            return false;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(organizerTokenHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(organizerToken.Trim()));

        return expectedHash.Length == actualHash.Length && CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static bool VerifyPassword(string? password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromHexString(parts[2]);
            expectedHash = Convert.FromHexString(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password.Trim(),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return expectedHash.Length == actualHash.Length && CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private sealed record SessionStartedAuditPayload(Guid SessionId, Guid QuizId);

    private sealed record SessionCancelledAuditPayload(Guid SessionId, Guid QuizId);

    private sealed record SessionCancelledOnStartupAuditPayload(Guid SessionId, Guid QuizId);

    private sealed record SessionResultsPublishedAuditPayload(Guid SessionId, Guid QuizId);

    private sealed record CurrentQuestionAnswerRevealedAuditPayload(Guid SessionId, Guid QuizId, Guid QuestionId);
}

public sealed record JoinSessionOperationResult(
    JoinSessionResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static JoinSessionOperationResult Success(JoinSessionResponse response) => new(response, null);

    public static JoinSessionOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record SessionStateOperationResult(
    SessionStateSnapshotResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static SessionStateOperationResult Success(SessionStateSnapshotResponse response) => new(response, null);

    public static SessionStateOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record LeaveSessionOperationResult(
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static LeaveSessionOperationResult Success() => new(Error: null);

    public static LeaveSessionOperationResult Fail(ApiErrorResponse error) => new(error);
}

public sealed record OrganizerSessionStateOperationResult(
    OrganizerSessionSnapshotResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static OrganizerSessionStateOperationResult Success(OrganizerSessionSnapshotResponse response) => new(response, null);

    public static OrganizerSessionStateOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record SubmitAnswerOperationResult(
    SubmitAnswerResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static SubmitAnswerOperationResult Success(SubmitAnswerResponse response) => new(response, null);

    public static SubmitAnswerOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record SessionResultsOperationResult(
    SessionResultsResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static SessionResultsOperationResult Success(SessionResultsResponse response) => new(response, null);

    public static SessionResultsOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record CorrectAnswersOperationResult(
    CorrectAnswersResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static CorrectAnswersOperationResult Success(CorrectAnswersResponse response) => new(response, null);

    public static CorrectAnswersOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record CurrentQuestionCorrectAnswerOperationResult(
    CurrentQuestionCorrectAnswerResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static CurrentQuestionCorrectAnswerOperationResult Success(CurrentQuestionCorrectAnswerResponse response) => new(response, null);

    public static CurrentQuestionCorrectAnswerOperationResult Fail(ApiErrorResponse error) => new(null, error);
}
