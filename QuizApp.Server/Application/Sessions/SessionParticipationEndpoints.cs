using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Server.Application.Sessions;

public static class SessionParticipationEndpoints
{
    private const string TeamReconnectTokenHeaderName = "X-Team-Reconnect-Token";
    private const string OrganizerTokenHeaderName = "X-Organizer-Token";
    private const string QuizPasswordHeaderName = "X-Quiz-Password";

    public static IEndpointRouteBuilder MapSessionParticipationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/sessions");

        group.MapPost("/join", JoinSessionAsync)
            .RequireRateLimiting("JoinPerIp");
        group.MapGet("/{sessionId:guid}", GetOrganizerSessionAsync);
        group.MapGet("/{sessionId:guid}/state", GetSessionStateAsync);
        group.MapPost("/{sessionId:guid}/answers", SubmitAnswerAsync)
            .RequireRateLimiting("SubmitPerTeam");
        group.MapDelete("/{sessionId:guid}/teams/{teamId:guid}", LeaveSessionAsync)
            .RequireRateLimiting("SubmitPerTeam");
        group.MapPost("/{sessionId:guid}/start", StartSessionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPost("/{sessionId:guid}/start-without-timer", StartSessionWithoutTimerAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPost("/{sessionId:guid}/pause", PauseSessionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPost("/{sessionId:guid}/resume", ResumeSessionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPost("/{sessionId:guid}/advance", AdvanceSessionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPost("/{sessionId:guid}/cancel", CancelSessionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapGet("/{sessionId:guid}/results", GetSessionResultsAsync);
        group.MapGet("/{sessionId:guid}/correct-answers", GetCorrectAnswersAsync);
        group.MapGet("/{sessionId:guid}/current-correct-answer", GetCurrentCorrectAnswerAsync);

        return endpoints;
    }

    private static async Task<IResult> JoinSessionAsync(
        JoinSessionRequest request,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var result = await sessionParticipationService.JoinSessionAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> GetCurrentCorrectAnswerAsync(
        Guid sessionId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.GetCurrentCorrectAnswerAsync(sessionId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> LeaveSessionAsync(
        Guid sessionId,
        Guid teamId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var teamReconnectToken = ReadHeader(httpContext, TeamReconnectTokenHeaderName);
        var result = await sessionParticipationService.LeaveSessionAsync(sessionId, teamId, teamReconnectToken, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> PauseSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.PauseSessionAsync(sessionId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> ResumeSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.ResumeSessionAsync(sessionId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> SubmitAnswerAsync(
        Guid sessionId,
        SubmitAnswerRequest request,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var teamReconnectToken = ReadHeader(httpContext, TeamReconnectTokenHeaderName);
        var result = await sessionParticipationService.SubmitAnswerAsync(sessionId, request, teamReconnectToken, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> StartSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.StartSessionAsync(sessionId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> StartSessionWithoutTimerAsync(
        Guid sessionId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.StartSessionAsync(sessionId, organizerToken, organizerPassword, cancellationToken, useQuestionTimer: false);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> AdvanceSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.AdvanceSessionAsync(sessionId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> CancelSessionAsync(
        Guid sessionId,
        CancelSessionRequest request,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.CancelSessionAsync(sessionId, organizerToken, organizerPassword, request.ConfirmCancellation, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> GetOrganizerSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.GetOrganizerSessionStateAsync(sessionId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> GetSessionStateAsync(
        Guid sessionId,
        Guid teamId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var teamReconnectToken = ReadHeader(httpContext, TeamReconnectTokenHeaderName);
        var result = await sessionParticipationService.GetSessionStateAsync(sessionId, teamId, teamReconnectToken, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> GetSessionResultsAsync(
        Guid sessionId,
        Guid? teamId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var teamReconnectToken = ReadHeader(httpContext, TeamReconnectTokenHeaderName);
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.GetSessionResultsAsync(sessionId, teamId, teamReconnectToken, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> GetCorrectAnswersAsync(
        Guid sessionId,
        Guid? teamId,
        HttpContext httpContext,
        ISessionParticipationService sessionParticipationService,
        CancellationToken cancellationToken)
    {
        var teamReconnectToken = ReadHeader(httpContext, TeamReconnectTokenHeaderName);
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await sessionParticipationService.GetCorrectAnswersAsync(sessionId, teamId, teamReconnectToken, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static string? ReadHeader(HttpContext httpContext, string headerName)
    {
        if (httpContext.Request.Headers.TryGetValue(headerName, out var headerValue))
        {
            return headerValue.ToString();
        }

        return null;
    }

    private static int ResolveStatusCode(ApiErrorResponse error)
    {
        return error.Code switch
        {
            ApiErrorCode.ValidationFailed => StatusCodes.Status400BadRequest,
            ApiErrorCode.CsvValidationFailed => StatusCodes.Status400BadRequest,
            ApiErrorCode.MissingAuthToken => StatusCodes.Status401Unauthorized,
            ApiErrorCode.InvalidAuthToken => StatusCodes.Status403Forbidden,
            ApiErrorCode.ResourceNotFound => StatusCodes.Status404NotFound,
            ApiErrorCode.TeamNameAlreadyUsed => StatusCodes.Status409Conflict,
            ApiErrorCode.QuestionClosed => StatusCodes.Status409Conflict,
            ApiErrorCode.AlreadyAnswered => StatusCodes.Status409Conflict,
            ApiErrorCode.SessionStateChanged => StatusCodes.Status409Conflict,
            ApiErrorCode.QuizHasActiveSessions => StatusCodes.Status409Conflict,
            ApiErrorCode.QuizHasNoQuestions => StatusCodes.Status409Conflict,
            ApiErrorCode.RateLimited => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest
        };
    }
}
