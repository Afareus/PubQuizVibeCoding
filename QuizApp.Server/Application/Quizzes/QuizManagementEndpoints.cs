using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Server.Application.Quizzes;

public static class QuizManagementEndpoints
{
    private const string OrganizerTokenHeaderName = "X-Organizer-Token";
    private const string QuizPasswordHeaderName = "X-Quiz-Password";

    public static IEndpointRouteBuilder MapQuizManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/quizzes");

        group.MapPost("/", CreateQuizAsync);
        group.MapPost("/{quizId:guid}/sessions", CreateSessionAsync);
        group.MapPost("/{quizId:guid}/import-csv", ImportQuizCsvAsync);
        group.MapGet("/{quizId:guid}", GetQuizDetailAsync);
        group.MapDelete("/{quizId:guid}", DeleteQuizAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateQuizAsync(
        CreateQuizRequest request,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var result = await quizManagementService.CreateQuizAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Created($"/api/quizzes/{result.Response!.QuizId}", result.Response);
    }

    private static async Task<IResult> ImportQuizCsvAsync(
        Guid quizId,
        ImportQuizCsvRequest request,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.ImportQuizCsvAsync(quizId, organizerToken, organizerPassword, request.CsvContent, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> CreateSessionAsync(
        Guid quizId,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.CreateSessionAsync(quizId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Created($"/api/sessions/{result.Response!.SessionId}", result.Response);
    }

    private static async Task<IResult> GetQuizDetailAsync(
        Guid quizId,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.GetQuizDetailAsync(quizId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> DeleteQuizAsync(
        Guid quizId,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeader(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.DeleteQuizAsync(quizId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.NoContent();
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
