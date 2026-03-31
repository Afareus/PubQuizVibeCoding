using QuizApp.Shared.Enums;

namespace QuizApp.Server.Domain.Entities;

public sealed class QuizSession
{
    private readonly List<Team> _teams = [];
    private readonly List<TeamAnswer> _answers = [];
    private readonly List<SessionResult> _results = [];
    private readonly List<AuditLog> _auditLogs = [];

    private QuizSession()
    {
    }

    private QuizSession(Guid sessionId, Guid quizId, string joinCode, DateTime createdAtUtc)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session id must not be empty.", nameof(sessionId));
        }

        if (quizId == Guid.Empty)
        {
            throw new ArgumentException("Quiz id must not be empty.", nameof(quizId));
        }

        SessionId = sessionId;
        QuizId = quizId;
        JoinCode = EntityGuards.Required(joinCode, nameof(joinCode), "Join code is required.").ToUpperInvariant();
        Status = SessionStatus.Waiting;
        CreatedAtUtc = EntityGuards.Utc(createdAtUtc, nameof(createdAtUtc));
        CurrentQuestionIndex = null;
    }

    public Guid SessionId { get; private set; }

    public Guid QuizId { get; private set; }

    public string JoinCode { get; private set; } = string.Empty;

    public SessionStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? QuestionDeadlineUtc { get; private set; }

    public string ConcurrencyToken { get; private set; } = Guid.NewGuid().ToString("N");

    public DateTime? FinishedAtUtc { get; private set; }

    public DateTime? EndedAtUtc { get; private set; }

    public int? CurrentQuestionIndex { get; private set; }

    public DateTime? CurrentQuestionStartedAtUtc { get; private set; }

    public DateTime? PausedAtUtc { get; private set; }

    public Quiz? Quiz { get; private set; }

    public IReadOnlyCollection<Team> Teams => _teams;

    public IReadOnlyCollection<TeamAnswer> Answers => _answers;

    public IReadOnlyCollection<SessionResult> Results => _results;

    public IReadOnlyCollection<AuditLog> AuditLogs => _auditLogs;

    public static QuizSession Create(Guid sessionId, Guid quizId, string joinCode, DateTime createdAtUtc)
    {
        return new QuizSession(sessionId, quizId, joinCode, createdAtUtc);
    }

    public void Start(DateTime startedAtUtc)
    {
        if (Status != SessionStatus.Waiting)
        {
            throw new InvalidOperationException("Session can only be started from waiting state.");
        }

        Status = SessionStatus.Running;
        StartedAtUtc = EntityGuards.Utc(startedAtUtc, nameof(startedAtUtc));
        PausedAtUtc = null;
        RefreshConcurrencyToken();
    }

    public void Pause(DateTime pausedAtUtc)
    {
        if (Status != SessionStatus.Running)
        {
            throw new InvalidOperationException("Session can only be paused from running state.");
        }

        if (!CurrentQuestionIndex.HasValue || !CurrentQuestionStartedAtUtc.HasValue || !QuestionDeadlineUtc.HasValue)
        {
            throw new InvalidOperationException("Session can only be paused with an active question.");
        }

        Status = SessionStatus.Paused;
        PausedAtUtc = EntityGuards.Utc(pausedAtUtc, nameof(pausedAtUtc));
        RefreshConcurrencyToken();
    }

    public void Resume(DateTime resumedAtUtc)
    {
        if (Status != SessionStatus.Paused)
        {
            throw new InvalidOperationException("Session can only be resumed from paused state.");
        }

        if (!PausedAtUtc.HasValue || !CurrentQuestionStartedAtUtc.HasValue || !QuestionDeadlineUtc.HasValue)
        {
            throw new InvalidOperationException("Paused session is missing timing metadata.");
        }

        var resumedAt = EntityGuards.Utc(resumedAtUtc, nameof(resumedAtUtc));
        var pauseDuration = resumedAt - PausedAtUtc.Value;
        if (pauseDuration < TimeSpan.Zero)
        {
            pauseDuration = TimeSpan.Zero;
        }

        CurrentQuestionStartedAtUtc = CurrentQuestionStartedAtUtc.Value.Add(pauseDuration);
        QuestionDeadlineUtc = QuestionDeadlineUtc.Value.Add(pauseDuration);
        PausedAtUtc = null;
        Status = SessionStatus.Running;
        RefreshConcurrencyToken();
    }

    public void SetCurrentQuestion(int currentQuestionIndex, DateTime currentQuestionStartedAtUtc, DateTime questionDeadlineUtc)
    {
        if (Status != SessionStatus.Running)
        {
            throw new InvalidOperationException("Current question can only be set when session is running.");
        }

        CurrentQuestionIndex = EntityGuards.Range(currentQuestionIndex, 0, int.MaxValue, nameof(currentQuestionIndex), "Current question index must be non-negative.");
        CurrentQuestionStartedAtUtc = EntityGuards.Utc(currentQuestionStartedAtUtc, nameof(currentQuestionStartedAtUtc));
        QuestionDeadlineUtc = EntityGuards.Utc(questionDeadlineUtc, nameof(questionDeadlineUtc));
        RefreshConcurrencyToken();
    }

    public void Finish(DateTime finishedAtUtc)
    {
        if (Status is SessionStatus.Finished or SessionStatus.Cancelled)
        {
            throw new InvalidOperationException("Terminal session states cannot be mutated.");
        }

        Status = SessionStatus.Finished;
        FinishedAtUtc = EntityGuards.Utc(finishedAtUtc, nameof(finishedAtUtc));
        EndedAtUtc = FinishedAtUtc;
        PausedAtUtc = null;
        ReleaseJoinCode();
        RefreshConcurrencyToken();
    }

    public void Cancel(DateTime cancelledAtUtc)
    {
        if (Status is SessionStatus.Finished or SessionStatus.Cancelled)
        {
            throw new InvalidOperationException("Terminal session states cannot be mutated.");
        }

        Status = SessionStatus.Cancelled;
        EndedAtUtc = EntityGuards.Utc(cancelledAtUtc, nameof(cancelledAtUtc));
        PausedAtUtc = null;
        ReleaseJoinCode();
        RefreshConcurrencyToken();
    }

    private void ReleaseJoinCode()
    {
        JoinCode = $"ENDED{SessionId:N}"[..16].ToUpperInvariant();
    }

    private void RefreshConcurrencyToken()
    {
        ConcurrencyToken = Guid.NewGuid().ToString("N");
    }
}