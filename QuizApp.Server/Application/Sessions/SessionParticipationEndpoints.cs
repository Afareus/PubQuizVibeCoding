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

        group.MapPost("/join", JoinSessionAsync);
        group.MapGet("/{sessionId:guid}", GetOrganizerSessionAsync);
        group.MapGet("/{sessionId:guid}/state", GetSessionStateAsync);
        group.MapPost("/{sessionId:guid}/start", StartSessionAsync);
        group.MapPost("/{sessionId:guid}/cancel", CancelSessionAsync);

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
