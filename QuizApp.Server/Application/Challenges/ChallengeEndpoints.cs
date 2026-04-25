using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Server.Application.Challenges;

public static class ChallengeEndpoints
{
    public static IEndpointRouteBuilder MapChallengeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/challenges");

        group.MapGet("/template", GetTemplateAsync);
        group.MapPost("/", CreateChallengeAsync)
            .RequireRateLimiting("OrganizerMutations");
        group.MapGet("/{publicCode}", GetChallengeAsync);
        group.MapPost("/{publicCode}/submissions", SubmitAnswersAsync)
            .RequireRateLimiting("SubmitPerTeam");
        group.MapGet("/{publicCode}/leaderboard", GetLeaderboardAsync);
        group.MapGet("/{publicCode}/submissions/{submissionId:guid}", GetSubmissionResultAsync);

        return endpoints;
    }

    private static IResult GetTemplateAsync(IChallengeService challengeService)
    {
        var response = challengeService.GetTemplate();
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> CreateChallengeAsync(
        CreateChallengeRequest request,
        IChallengeService challengeService,
        CancellationToken cancellationToken)
    {
        var result = await challengeService.CreateChallengeAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ErrorResult(result.Error!, isNotFound: false);
        }

        return TypedResults.Created($"/api/challenges/{result.Response!.PublicCode}", result.Response);
    }

    private static async Task<IResult> GetChallengeAsync(
        string publicCode,
        IChallengeService challengeService,
        CancellationToken cancellationToken)
    {
        var result = await challengeService.GetChallengeAsync(publicCode, cancellationToken);
        if (!result.IsSuccess)
        {
            return ErrorResult(result.Error!, isNotFound: true);
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> SubmitAnswersAsync(
        string publicCode,
        SubmitChallengeAnswersRequest request,
        IChallengeService challengeService,
        CancellationToken cancellationToken)
    {
        var result = await challengeService.SubmitAnswersAsync(publicCode, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ErrorResult(result.Error!, isNotFound: result.Error!.Contains("nenalezena"));
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> GetLeaderboardAsync(
        string publicCode,
        IChallengeService challengeService,
        CancellationToken cancellationToken)
    {
        var result = await challengeService.GetLeaderboardAsync(publicCode, cancellationToken);
        if (!result.IsSuccess)
        {
            return ErrorResult(result.Error!, isNotFound: true);
        }

        return TypedResults.Ok(result.Response!);
    }

    private static async Task<IResult> GetSubmissionResultAsync(
        string publicCode,
        Guid submissionId,
        IChallengeService challengeService,
        CancellationToken cancellationToken)
    {
        var result = await challengeService.GetSubmissionResultAsync(publicCode, submissionId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ErrorResult(result.Error!, isNotFound: true);
        }

        return TypedResults.Ok(result.Response!);
    }

    private static IResult ErrorResult(string message, bool isNotFound)
    {
        var statusCode = isNotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        var code = isNotFound ? ApiErrorCode.ResourceNotFound : ApiErrorCode.ValidationFailed;
        var error = new ApiErrorResponse(code, message);
        return TypedResults.Json(error, statusCode: statusCode);
    }
}
