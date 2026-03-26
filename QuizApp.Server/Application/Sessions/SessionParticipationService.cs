using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuizApp.Server.Domain.Entities;
using QuizApp.Server.Persistence;
using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Server.Application.Sessions;

public interface ISessionParticipationService
{
    Task<JoinSessionOperationResult> JoinSessionAsync(JoinSessionRequest request, CancellationToken cancellationToken);

    Task<SessionStateOperationResult> GetSessionStateAsync(Guid sessionId, Guid teamId, string? teamReconnectToken, CancellationToken cancellationToken);

    Task<OrganizerSessionStateOperationResult> GetOrganizerSessionStateAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<OrganizerSessionStateOperationResult> StartSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<OrganizerSessionStateOperationResult> CancelSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, bool confirmCancellation, CancellationToken cancellationToken);

    Task ProgressDueSessionsAsync(CancellationToken cancellationToken);
}

public sealed class SessionParticipationService : ISessionParticipationService
{
    private const int TeamReconnectTokenEntropyBytes = 32;
    private const int MaxTeamsPerSession = 20;

    private readonly QuizAppDbContext _dbContext;
    private readonly ISessionRealtimePublisher _sessionRealtimePublisher;

    public SessionParticipationService(QuizAppDbContext dbContext, ISessionRealtimePublisher sessionRealtimePublisher)
    {
        _dbContext = dbContext;
        _sessionRealtimePublisher = sessionRealtimePublisher;
    }

    public async Task<JoinSessionOperationResult> JoinSessionAsync(JoinSessionRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateJoinSessionRequest(request);
        if (validationErrors is not null)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Vstupní data nejsou validní.", validationErrors));
        }

        var normalizedJoinCode = request.JoinCode.Trim().ToUpperInvariant();
        var session = await _dbContext.Sessions
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.JoinCode == normalizedJoinCode, cancellationToken);

        if (session is null)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session pro zadaný join code nebyla nalezena."));
        }

        if (session.Status != SessionStatus.Waiting)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Do session se lze připojit pouze ve stavu WAITING."));
        }

        if (session.Teams.Count >= MaxTeamsPerSession)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Session již obsahuje maximální počet týmů (20)."));
        }

        var normalizedTeamName = request.TeamName.Trim().ToUpperInvariant();
        if (session.Teams.Any(team => string.Equals(team.NormalizedTeamName, normalizedTeamName, StringComparison.Ordinal)))
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.TeamNameAlreadyUsed, "Název týmu už je v této session použit."));
        }

        var reconnectToken = GenerateTeamReconnectToken();
        var nowUtc = DateTime.UtcNow;

        var team = Team.Create(
            Guid.NewGuid(),
            session.SessionId,
            request.TeamName.Trim(),
            HashTeamReconnectToken(reconnectToken),
            nowUtc);

        _dbContext.Teams.Add(team);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return JoinSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.TeamNameAlreadyUsed, "Název týmu už je v této session použit."));
        }

        await _sessionRealtimePublisher.PublishSessionEventAsync(session.SessionId, RealtimeEventName.TeamJoined, cancellationToken);

        return JoinSessionOperationResult.Success(new JoinSessionResponse(
            session.SessionId,
            team.TeamId,
            reconnectToken,
            session.Status));
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
                    question.Options
                        .OrderBy(x => x.OptionKey)
                        .Select(x => new SnapshotQuestionOptionDto(x.OptionKey, x.Text))
                        .ToList());
            }
        }

        var response = new SessionStateSnapshotResponse(
            session.SessionId,
            session.Status,
            session.CurrentQuestionIndex,
            ToUtcOffset(session.CurrentQuestionStartedAtUtc),
            ToUtcOffset(session.QuestionDeadlineUtc),
            currentQuestion,
            session.Teams
                .OrderBy(x => x.JoinedAtUtc)
                .Select(x => new SnapshotTeamDto(x.TeamId, x.Name))
                .ToList());

        return SessionStateOperationResult.Success(response);
    }

    public async Task<OrganizerSessionStateOperationResult> GetOrganizerSessionStateAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .AsNoTracking()
            .Include(x => x.Quiz)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (!TryAuthorizeOrganizer(session.Quiz, organizerToken, organizerPassword, out var authError))
        {
            return OrganizerSessionStateOperationResult.Fail(authError!);
        }

        var response = new OrganizerSessionSnapshotResponse(
            session.SessionId,
            session.QuizId,
            session.JoinCode,
            session.Status,
            new DateTimeOffset(session.CreatedAtUtc, TimeSpan.Zero),
            ToUtcOffset(session.StartedAtUtc),
            ToUtcOffset(session.EndedAtUtc),
            session.Teams
                .OrderBy(x => x.JoinedAtUtc)
                .Select(x => new SnapshotTeamDto(x.TeamId, x.Name))
                .ToList());

        return OrganizerSessionStateOperationResult.Success(response);
    }

    public async Task<OrganizerSessionStateOperationResult> StartSessionAsync(Guid sessionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .Include(x => x.Quiz)
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (!TryAuthorizeOrganizer(session.Quiz, organizerToken, organizerPassword, out var authError))
        {
            return OrganizerSessionStateOperationResult.Fail(authError!);
        }

        if (session.Status != SessionStatus.Waiting)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session lze spustit pouze ze stavu WAITING."));
        }

        if (session.Teams.Count == 0)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.SessionStateChanged, "Session nelze spustit bez připojeného týmu."));
        }

        var nowUtc = DateTime.UtcNow;
        session.Start(nowUtc);

        var firstQuestion = session.Quiz!.Questions
            .OrderBy(x => x.OrderIndex)
            .FirstOrDefault();

        if (firstQuestion is not null)
        {
            session.SetCurrentQuestion(
                firstQuestion.OrderIndex,
                nowUtc,
                nowUtc.AddSeconds(firstQuestion.TimeLimitSec));
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

    public async Task ProgressDueSessionsAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var emittedEvents = new List<(Guid SessionId, RealtimeEventName EventName)>();
        var candidateSessions = await _dbContext.Sessions
            .Include(x => x.Quiz!)
                .ThenInclude(x => x.Questions)
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
                emittedEvents.Add((session.SessionId, RealtimeEventName.SessionFinished));
                emittedEvents.Add((session.SessionId, RealtimeEventName.ResultsReady));
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
            .Include(x => x.Teams)
            .SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return OrganizerSessionStateOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Session nebyla nalezena."));
        }

        if (!TryAuthorizeOrganizer(session.Quiz, organizerToken, organizerPassword, out var authError))
        {
            return OrganizerSessionStateOperationResult.Fail(authError!);
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

    private static OrganizerSessionSnapshotResponse ToOrganizerSnapshot(QuizSession session)
    {
        return new OrganizerSessionSnapshotResponse(
            session.SessionId,
            session.QuizId,
            session.JoinCode,
            session.Status,
            new DateTimeOffset(session.CreatedAtUtc, TimeSpan.Zero),
            ToUtcOffset(session.StartedAtUtc),
            ToUtcOffset(session.EndedAtUtc),
            session.Teams
                .OrderBy(x => x.JoinedAtUtc)
                .Select(x => new SnapshotTeamDto(x.TeamId, x.Name))
                .ToList());
    }

    private static DateTimeOffset? ToUtcOffset(DateTime? value)
    {
        return value is null ? null : new DateTimeOffset(value.Value, TimeSpan.Zero);
    }

    private static IReadOnlyDictionary<string, string[]>? ValidateJoinSessionRequest(JoinSessionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.JoinCode))
        {
            errors[nameof(JoinSessionRequest.JoinCode)] = ["Join code je povinný."];
        }

        if (string.IsNullOrWhiteSpace(request.TeamName))
        {
            errors[nameof(JoinSessionRequest.TeamName)] = ["Název týmu je povinný."];
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

        error = new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatný organizer token nebo mazací heslo.");
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

public sealed record OrganizerSessionStateOperationResult(
    OrganizerSessionSnapshotResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static OrganizerSessionStateOperationResult Success(OrganizerSessionSnapshotResponse response) => new(response, null);

    public static OrganizerSessionStateOperationResult Fail(ApiErrorResponse error) => new(null, error);
}
