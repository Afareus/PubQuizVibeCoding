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

        group.MapGet("/", GetQuizzesAsync);
        group.MapPost("/", CreateQuizAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPost("/{quizId:guid}/sessions", CreateSessionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapGet("/{quizId:guid}/sessions/generate-join-code", GenerateJoinCodeAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPut("/{quizId:guid}/start-permission", UpdateQuizStartPermissionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPost("/{quizId:guid}/questions", AddQuestionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPut("/{quizId:guid}/questions/{questionId:guid}", UpdateQuestionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapDelete("/{quizId:guid}/questions/{questionId:guid}", DeleteQuestionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPut("/{quizId:guid}/questions/reorder", ReorderQuestionAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapPost("/{quizId:guid}/import-csv", ImportQuizCsvAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapGet("/{quizId:guid}", GetQuizDetailAsync);
        group.MapDelete("/{quizId:guid}", DeleteQuizAsync)
            .RequireRateLimiting("OrganizerMutations");

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

    private static async Task<IResult> GetQuizzesAsync(
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var response = await quizManagementService.GetQuizzesAsync(cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ImportQuizCsvAsync(
        Guid quizId,
        ImportQuizCsvRequest request,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.ImportQuizCsvAsync(quizId, organizerToken, organizerPassword, request.CsvContent, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> UpdateQuizStartPermissionAsync(
        Guid quizId,
        UpdateQuizStartPermissionRequest request,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.UpdateQuizStartPermissionAsync(quizId, request, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> DeleteQuestionAsync(
        Guid quizId,
        Guid questionId,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.DeleteQuestionAsync(quizId, questionId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ReorderQuestionAsync(
        Guid quizId,
        ReorderQuizQuestionRequest request,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.ReorderQuestionAsync(quizId, request, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> CreateSessionAsync(
        Guid quizId,
        CreateSessionRequest request,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.CreateSessionAsync(quizId, request, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Created($"/api/sessions/{result.Response!.SessionId}", result.Response);
    }

    private static async Task<IResult> GenerateJoinCodeAsync(
        Guid quizId,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.GenerateJoinCodeAsync(quizId, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> AddQuestionAsync(
        Guid quizId,
        AddQuizQuestionRequest request,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.AddQuestionAsync(quizId, request, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Created($"/api/quizzes/{quizId}", result.Response!);
    }

    private static async Task<IResult> UpdateQuestionAsync(
        Guid quizId,
        Guid questionId,
        UpdateQuizQuestionRequest request,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

        var result = await quizManagementService.UpdateQuestionAsync(quizId, questionId, request, organizerToken, organizerPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.Json(result.Error!, statusCode: ResolveStatusCode(result.Error!));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> GetQuizDetailAsync(
        Guid quizId,
        HttpContext httpContext,
        IQuizManagementService quizManagementService,
        CancellationToken cancellationToken)
    {
        var organizerToken = ReadHeader(httpContext, OrganizerTokenHeaderName);
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

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
        var organizerPassword = ReadHeaderBase64Decoded(httpContext, QuizPasswordHeaderName);

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

    private static string? ReadHeaderBase64Decoded(HttpContext httpContext, string headerName)
    {
        if (!httpContext.Request.Headers.TryGetValue(headerName, out var headerValue))
        {
            return null;
        }

        var raw = headerValue.ToString();
        try
        {
            var bytes = Convert.FromBase64String(raw);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return raw;
        }
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
            ApiErrorCode.QuizStartLocked => StatusCodes.Status409Conflict,
            ApiErrorCode.RateLimited => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest
        };
    }
}
