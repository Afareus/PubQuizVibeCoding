using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using QuizApp.Server.Application.Common;
using QuizApp.Server.Application.Challenges;
using QuizApp.Server.Domain.Entities;
using QuizApp.Server.Persistence;
using QuizApp.Shared.Contracts;

namespace QuizApp.Server.Application.Challenges;

public interface IChallengeService
{
    GetChallengeTemplateResponse GetTemplate();

    Task<CreateChallengeOperationResult> CreateChallengeAsync(CreateChallengeRequest request, CancellationToken cancellationToken);

    Task<GetChallengeOperationResult> GetChallengeAsync(string publicCode, CancellationToken cancellationToken);

    Task<SubmitChallengeAnswersOperationResult> SubmitAnswersAsync(string publicCode, SubmitChallengeAnswersRequest request, CancellationToken cancellationToken);

    Task<GetChallengeLeaderboardOperationResult> GetLeaderboardAsync(string publicCode, CancellationToken cancellationToken);

    Task<GetSubmissionResultOperationResult> GetSubmissionResultAsync(string publicCode, Guid submissionId, CancellationToken cancellationToken);
}

// --- Operation result types ---

public sealed record CreateChallengeOperationResult(bool IsSuccess, CreateChallengeResponse? Response, string? Error);
public sealed record GetChallengeOperationResult(bool IsSuccess, GetChallengeResponse? Response, string? Error);
public sealed record SubmitChallengeAnswersOperationResult(bool IsSuccess, SubmitChallengeAnswersResponse? Response, string? Error);
public sealed record GetChallengeLeaderboardOperationResult(bool IsSuccess, ChallengeLeaderboardResponse? Response, string? Error);
public sealed record GetSubmissionResultOperationResult(bool IsSuccess, GetChallengeSubmissionResultResponse? Response, string? Error);

// --- Service ---

public sealed class ChallengeService : IChallengeService
{
    private const int MaxCreatorNameLength = 100;
    private const int MaxParticipantNameLength = 100;
    private const int MaxTitleLength = 200;
    private const int PublicCodeLength = 10;
    private const int PublicCodeMaxAttempts = 20;
    private const int LeaderboardTopN = 20;
    private const string PublicCodeAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    private readonly QuizAppDbContext _dbContext;

    public ChallengeService(QuizAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // --- Template ---

    public GetChallengeTemplateResponse GetTemplate()
    {
        return new GetChallengeTemplateResponse(
            ChallengeTemplate.Questions
                .Select(q => new ChallengeTemplateQuestionDto(
                    q.TemplateQuestionId,
                    q.OrderIndex,
                    q.Text,
                    q.Options.Select(o => new ChallengeTemplateOptionDto(o.OptionKey, o.Text)).ToList()))
                .ToList());
    }

    // --- Create ---

    public async Task<CreateChallengeOperationResult> CreateChallengeAsync(CreateChallengeRequest request, CancellationToken cancellationToken)
    {
        var creatorName = TextInputSanitizer.SanitizeSingleLine(request.CreatorName);
        var title = TextInputSanitizer.SanitizeSingleLine(request.Title);

        if (string.IsNullOrEmpty(creatorName))
            return Fail("Jméno tvůrce je povinné.");
        if (creatorName.Length > MaxCreatorNameLength)
            return Fail($"Jméno tvůrce nesmí být delší než {MaxCreatorNameLength} znaků.");
        if (string.IsNullOrEmpty(title))
            return Fail("Název challenge je povinný.");
        if (title.Length > MaxTitleLength)
            return Fail($"Název challenge nesmí být delší než {MaxTitleLength} znaků.");

        var template = ChallengeTemplate.Questions;

        if (request.Answers is null || request.Answers.Count != template.Count)
            return Fail($"Challenge musí obsahovat právě {template.Count} odpovědí.");

        foreach (var templateQuestion in template)
        {
            var answer = request.Answers.FirstOrDefault(a => a.TemplateQuestionId == templateQuestion.TemplateQuestionId);
            if (answer is null)
                return Fail($"Chybí odpověď pro otázku {templateQuestion.TemplateQuestionId}.");

            var validKeys = templateQuestion.Options.Select(o => o.OptionKey).ToHashSet();
            if (!validKeys.Contains(answer.SelectedOptionKey))
                return Fail($"Neplatná volba '{answer.SelectedOptionKey}' pro otázku {templateQuestion.TemplateQuestionId}.");
        }

        var publicCode = await GenerateUniquePublicCodeAsync(cancellationToken);
        if (publicCode is null)
            return Fail("Nepodařilo se vygenerovat unikátní kód. Zkuste to prosím znovu.");

        var challengeId = Guid.NewGuid();
        var challenge = new Challenge(challengeId, publicCode, title, creatorName, DateTime.UtcNow);

        foreach (var tq in template.OrderBy(q => q.OrderIndex))
        {
            var selectedKey = request.Answers.First(a => a.TemplateQuestionId == tq.TemplateQuestionId).SelectedOptionKey;
            var questionId = Guid.NewGuid();
            var question = new ChallengeQuestion(questionId, challengeId, tq.OrderIndex, tq.Text, selectedKey);

            foreach (var opt in tq.Options)
            {
                var option = new ChallengeAnswerOption(Guid.NewGuid(), questionId, opt.OptionKey, opt.Text);
                _dbContext.ChallengeAnswerOptions.Add(option);
            }

            _dbContext.ChallengeQuestions.Add(question);
        }

        _dbContext.Challenges.Add(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateChallengeOperationResult(
            true,
            new CreateChallengeResponse(challengeId, publicCode, title, creatorName),
            null);
    }

    // --- Get for playing ---

    public async Task<GetChallengeOperationResult> GetChallengeAsync(string publicCode, CancellationToken cancellationToken)
    {
        var challenge = await _dbContext.Challenges
            .AsNoTracking()
            .Include(c => c.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(c => c.PublicCode == publicCode && !c.IsDeleted, cancellationToken);

        if (challenge is null)
            return new GetChallengeOperationResult(false, null, "Challenge nenalezena.");

        var questions = challenge.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new ChallengeQuestionDto(
                q.ChallengeQuestionId,
                q.OrderIndex,
                q.Text,
                q.Options
                    .OrderBy(o => o.OptionKey)
                    .Select(o => new ChallengeOptionDto(o.OptionKey, o.Text))
                    .ToList()))
            .ToList();

        return new GetChallengeOperationResult(
            true,
            new GetChallengeResponse(challenge.PublicCode, challenge.Title, challenge.CreatorName, questions),
            null);
    }

    // --- Submit ---

    public async Task<SubmitChallengeAnswersOperationResult> SubmitAnswersAsync(string publicCode, SubmitChallengeAnswersRequest request, CancellationToken cancellationToken)
    {
        var participantName = TextInputSanitizer.SanitizeSingleLine(request.ParticipantName);

        if (string.IsNullOrEmpty(participantName))
            return new SubmitChallengeAnswersOperationResult(false, null, "Jméno hráče je povinné.");
        if (participantName.Length > MaxParticipantNameLength)
            return new SubmitChallengeAnswersOperationResult(false, null, $"Jméno hráče nesmí být delší než {MaxParticipantNameLength} znaků.");

        var challenge = await _dbContext.Challenges
            .Include(c => c.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(c => c.PublicCode == publicCode && !c.IsDeleted, cancellationToken);

        if (challenge is null)
            return new SubmitChallengeAnswersOperationResult(false, null, "Challenge nenalezena.");

        var questions = challenge.Questions.OrderBy(q => q.OrderIndex).ToList();

        if (request.Answers is null || request.Answers.Count != questions.Count)
            return new SubmitChallengeAnswersOperationResult(false, null, $"Je třeba odeslat právě {questions.Count} odpovědí.");

        foreach (var question in questions)
        {
            if (!request.Answers.Any(a => a.QuestionId == question.ChallengeQuestionId))
                return new SubmitChallengeAnswersOperationResult(false, null, $"Chybí odpověď pro otázku {question.ChallengeQuestionId}.");
        }

        var submissionId = Guid.NewGuid();
        var submittedAt = DateTime.UtcNow;
        int score = 0;

        var submissionAnswers = new List<ChallengeSubmissionAnswer>();

        foreach (var question in questions)
        {
            var playerAnswer = request.Answers.First(a => a.QuestionId == question.ChallengeQuestionId);
            var isCorrect = string.Equals(playerAnswer.SelectedOptionKey, question.CreatorSelectedOptionKey, StringComparison.Ordinal);
            if (isCorrect) score++;

            submissionAnswers.Add(new ChallengeSubmissionAnswer(
                Guid.NewGuid(),
                submissionId,
                question.ChallengeQuestionId,
                playerAnswer.SelectedOptionKey,
                isCorrect));
        }

        var maxScore = questions.Count;
        var submission = new ChallengeSubmission(submissionId, challenge.ChallengeId, participantName, score, maxScore, submittedAt);

        _dbContext.ChallengeSubmissions.Add(submission);
        _dbContext.ChallengeSubmissionAnswers.AddRange(submissionAnswers);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var leaderboard = await BuildLeaderboardEntriesAsync(challenge.ChallengeId, cancellationToken);
        var rank = leaderboard.First(e => e.ParticipantName == participantName && e.Score == score).Rank;

        return new SubmitChallengeAnswersOperationResult(
            true,
            new SubmitChallengeAnswersResponse(submissionId, score, maxScore, rank, leaderboard),
            null);
    }

    // --- Leaderboard ---

    public async Task<GetChallengeLeaderboardOperationResult> GetLeaderboardAsync(string publicCode, CancellationToken cancellationToken)
    {
        var challenge = await _dbContext.Challenges
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicCode == publicCode && !c.IsDeleted, cancellationToken);

        if (challenge is null)
            return new GetChallengeLeaderboardOperationResult(false, null, "Challenge nenalezena.");

        var entries = await BuildLeaderboardEntriesAsync(challenge.ChallengeId, cancellationToken);

        return new GetChallengeLeaderboardOperationResult(
            true,
            new ChallengeLeaderboardResponse(challenge.PublicCode, challenge.Title, challenge.CreatorName, entries),
            null);
    }

    // --- Submission result ---

    public async Task<GetSubmissionResultOperationResult> GetSubmissionResultAsync(string publicCode, Guid submissionId, CancellationToken cancellationToken)
    {
        var challenge = await _dbContext.Challenges
            .AsNoTracking()
            .Include(c => c.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(c => c.PublicCode == publicCode && !c.IsDeleted, cancellationToken);

        if (challenge is null)
            return new GetSubmissionResultOperationResult(false, null, "Challenge nenalezena.");

        var submission = await _dbContext.ChallengeSubmissions
            .AsNoTracking()
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.ChallengeSubmissionId == submissionId && s.ChallengeId == challenge.ChallengeId, cancellationToken);

        if (submission is null)
            return new GetSubmissionResultOperationResult(false, null, "Výsledek nenalezen.");

        var questionMap = challenge.Questions.ToDictionary(q => q.ChallengeQuestionId);

        var answerDtos = submission.Answers
            .Select(a =>
            {
                var question = questionMap[a.ChallengeQuestionId];
                return new ChallengeSubmissionResultAnswerDto(
                    a.ChallengeQuestionId,
                    question.OrderIndex,
                    question.Text,
                    a.SelectedOptionKey,
                    question.CreatorSelectedOptionKey,
                    a.IsCorrect);
            })
            .OrderBy(a => a.OrderIndex)
            .ToList();

        var leaderboard = await BuildLeaderboardEntriesAsync(challenge.ChallengeId, cancellationToken);
        var rank = leaderboard.FirstOrDefault(e => e.SubmittedAtUtc == new DateTimeOffset(submission.SubmittedAtUtc, TimeSpan.Zero))?.Rank ?? 0;

        return new GetSubmissionResultOperationResult(
            true,
            new GetChallengeSubmissionResultResponse(
                submission.ChallengeSubmissionId,
                submission.ParticipantName,
                submission.Score,
                submission.MaxScore,
                rank,
                answerDtos,
                leaderboard),
            null);
    }

    // --- Helpers ---

    private async Task<IReadOnlyList<ChallengeLeaderboardEntryDto>> BuildLeaderboardEntriesAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        var submissions = await _dbContext.ChallengeSubmissions
            .AsNoTracking()
            .Where(s => s.ChallengeId == challengeId)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.SubmittedAtUtc)
            .Take(LeaderboardTopN)
            .ToListAsync(cancellationToken);

        return submissions
            .Select((s, i) => new ChallengeLeaderboardEntryDto(
                i + 1,
                s.ParticipantName,
                s.Score,
                s.MaxScore,
                new DateTimeOffset(s.SubmittedAtUtc, TimeSpan.Zero)))
            .ToList();
    }

    private async Task<string?> GenerateUniquePublicCodeAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < PublicCodeMaxAttempts; attempt++)
        {
            var code = GeneratePublicCode();
            var exists = await _dbContext.Challenges.AnyAsync(c => c.PublicCode == code, cancellationToken);
            if (!exists)
                return code;
        }
        return null;
    }

    private static string GeneratePublicCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(PublicCodeLength);
        return new string(bytes.Select(b => PublicCodeAlphabet[b % PublicCodeAlphabet.Length]).ToArray());
    }

    private static CreateChallengeOperationResult Fail(string error) =>
        new(false, null, error);
}
