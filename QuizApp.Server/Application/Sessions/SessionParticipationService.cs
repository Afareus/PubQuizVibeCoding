using System.Security.Cryptography;
using System.Text;
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
}

public sealed class SessionParticipationService : ISessionParticipationService
{
    private const int TeamReconnectTokenEntropyBytes = 32;
    private const int MaxTeamsPerSession = 20;

    private readonly QuizAppDbContext _dbContext;

    public SessionParticipationService(QuizAppDbContext dbContext)
    {
        _dbContext = dbContext;
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
