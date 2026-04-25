using Microsoft.EntityFrameworkCore;
using QuizApp.Server.Application.Challenges;
using QuizApp.Server.Persistence;
using QuizApp.Shared.Contracts;

namespace QuizApp.Tests;

public class ChallengeServiceTests
{
    private static QuizAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<QuizAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new QuizAppDbContext(options);
    }

    private static ChallengeService CreateService(QuizAppDbContext dbContext) =>
        new(dbContext);

    private static CreateChallengeRequest BuildValidCreateRequest(string creatorName = "Adam", string title = "Jak dobře znáš Adama?")
    {
        var answers = ChallengeTemplate.Questions
            .Select(q => new CreateChallengeAnswerDto(q.TemplateQuestionId, q.Options[0].OptionKey))
            .ToList();
        return new CreateChallengeRequest(creatorName, title, answers);
    }

    // --- GetTemplate ---

    [Fact]
    public void GetTemplate_ReturnsExactlyTenQuestions()
    {
        var service = CreateService(CreateDbContext());
        var result = service.GetTemplate();
        Assert.Equal(10, result.Questions.Count);
    }

    [Fact]
    public void GetTemplate_EachQuestionHasFourOptions()
    {
        var service = CreateService(CreateDbContext());
        var result = service.GetTemplate();
        Assert.All(result.Questions, q => Assert.Equal(4, q.Options.Count));
    }

    // --- CreateChallenge ---

    [Fact]
    public async Task CreateChallengeAsync_ValidRequest_PersistsChallengeAndReturnsPublicCode()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateChallengeAsync(BuildValidCreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.NotNull(result.Response!.PublicCode);
        Assert.Equal(10, result.Response.PublicCode.Length);

        var stored = await dbContext.Challenges.SingleAsync(c => c.ChallengeId == result.Response.ChallengeId);
        Assert.Equal("Adam", stored.CreatorName);
        Assert.Equal("Jak dobře znáš Adama?", stored.Title);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task CreateChallengeAsync_PersistsTenQuestionsWithOptions()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateChallengeAsync(BuildValidCreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var questions = await dbContext.ChallengeQuestions
            .Where(q => q.ChallengeId == result.Response!.ChallengeId)
            .Include(q => q.Options)
            .ToListAsync();

        Assert.Equal(10, questions.Count);
        Assert.All(questions, q => Assert.Equal(4, q.Options.Count));
    }

    [Fact]
    public async Task CreateChallengeAsync_EmptyCreatorName_ReturnsError()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = BuildValidCreateRequest(creatorName: "  ");

        var result = await service.CreateChallengeAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task CreateChallengeAsync_WrongAnswerCount_ReturnsError()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new CreateChallengeRequest("Adam", "Kvíz", [new CreateChallengeAnswerDto(1, "A")]);

        var result = await service.CreateChallengeAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    // --- GetChallenge ---

    [Fact]
    public async Task GetChallengeAsync_ExistingCode_ReturnsChallengeWithoutCorrectAnswers()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateChallengeAsync(BuildValidCreateRequest(), CancellationToken.None);

        var result = await service.GetChallengeAsync(created.Response!.PublicCode, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(10, result.Response!.Questions.Count);
        // GetChallengeResponse must not expose CreatorSelectedOptionKey
        Assert.All(result.Response.Questions, q => Assert.Equal(4, q.Options.Count));
    }

    [Fact]
    public async Task GetChallengeAsync_UnknownCode_ReturnsError()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.GetChallengeAsync("neexistuje", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    // --- SubmitAnswers ---

    [Fact]
    public async Task SubmitAnswersAsync_AllCorrect_ReturnsMaxScore()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var answers = ChallengeTemplate.Questions
            .Select(q => new CreateChallengeAnswerDto(q.TemplateQuestionId, q.Options[0].OptionKey))
            .ToList();
        var created = await service.CreateChallengeAsync(
            new CreateChallengeRequest("Eva", "Kvíz", answers), CancellationToken.None);

        var challengeResponse = await service.GetChallengeAsync(created.Response!.PublicCode, CancellationToken.None);
        var playerAnswers = challengeResponse.Response!.Questions
            .Select(q => new SubmitChallengeAnswerDto(q.QuestionId, q.Options[0].OptionKey))
            .ToList();

        var submitResult = await service.SubmitAnswersAsync(
            created.Response.PublicCode,
            new SubmitChallengeAnswersRequest("Hráč1", playerAnswers),
            CancellationToken.None);

        Assert.True(submitResult.IsSuccess);
        Assert.Equal(10, submitResult.Response!.MaxScore);
        Assert.Equal(10, submitResult.Response.Score);
    }

    [Fact]
    public async Task SubmitAnswersAsync_AllWrong_ReturnsZeroScore()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        // Creator always picks option index 0, player always picks index 1
        var creatorAnswers = ChallengeTemplate.Questions
            .Select(q => new CreateChallengeAnswerDto(q.TemplateQuestionId, q.Options[0].OptionKey))
            .ToList();
        var created = await service.CreateChallengeAsync(
            new CreateChallengeRequest("Eva", "Kvíz", creatorAnswers), CancellationToken.None);

        var challengeResponse = await service.GetChallengeAsync(created.Response!.PublicCode, CancellationToken.None);
        var playerAnswers = challengeResponse.Response!.Questions
            .Select(q => new SubmitChallengeAnswerDto(q.QuestionId, q.Options[1].OptionKey))
            .ToList();

        var submitResult = await service.SubmitAnswersAsync(
            created.Response.PublicCode,
            new SubmitChallengeAnswersRequest("Hráč2", playerAnswers),
            CancellationToken.None);

        Assert.True(submitResult.IsSuccess);
        Assert.Equal(0, submitResult.Response!.Score);
        Assert.Equal(10, submitResult.Response.MaxScore);
    }

    [Fact]
    public async Task SubmitAnswersAsync_LeaderboardIsSortedByScoreDescThenTimeAsc()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateChallengeAsync(BuildValidCreateRequest(), CancellationToken.None);
        var challengeResponse = await service.GetChallengeAsync(created.Response!.PublicCode, CancellationToken.None);
        var questions = challengeResponse.Response!.Questions;

        async Task Submit(string name, int correctCount)
        {
            var playerAnswers = questions.Select((q, i) =>
                new SubmitChallengeAnswerDto(q.QuestionId, i < correctCount ? q.Options[0].OptionKey : q.Options[1].OptionKey))
                .ToList();
            await service.SubmitAnswersAsync(created.Response!.PublicCode,
                new SubmitChallengeAnswersRequest(name, playerAnswers), CancellationToken.None);
        }

        await Submit("Hráč_5", 5);
        await Submit("Hráč_8", 8);
        await Submit("Hráč_3", 3);

        var leaderboard = await service.GetLeaderboardAsync(created.Response!.PublicCode, CancellationToken.None);

        Assert.True(leaderboard.IsSuccess);
        var entries = leaderboard.Response!.Entries;
        Assert.Equal(1, entries[0].Rank);
        Assert.Equal("Hráč_8", entries[0].ParticipantName);
        Assert.Equal(2, entries[1].Rank);
        Assert.Equal("Hráč_5", entries[1].ParticipantName);
        Assert.Equal(3, entries[2].Rank);
        Assert.Equal("Hráč_3", entries[2].ParticipantName);
    }

    [Fact]
    public async Task SubmitAnswersAsync_EmptyParticipantName_ReturnsError()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateChallengeAsync(BuildValidCreateRequest(), CancellationToken.None);

        var challengeResponse = await service.GetChallengeAsync(created.Response!.PublicCode, CancellationToken.None);
        var playerAnswers = challengeResponse.Response!.Questions
            .Select(q => new SubmitChallengeAnswerDto(q.QuestionId, q.Options[0].OptionKey))
            .ToList();

        var result = await service.SubmitAnswersAsync(
            created.Response!.PublicCode,
            new SubmitChallengeAnswersRequest("   ", playerAnswers),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }
}
