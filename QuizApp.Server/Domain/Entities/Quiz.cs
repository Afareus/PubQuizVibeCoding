namespace QuizApp.Server.Domain.Entities;

public sealed class Quiz
{
    private readonly List<Question> _questions = [];
    private readonly List<QuizSession> _sessions = [];
    private readonly List<AuditLog> _auditLogs = [];

    private Quiz()
    {
    }

    private Quiz(Guid quizId, string name, string deletePasswordHash, string quizOrganizerTokenHash, DateTime createdAtUtc)
    {
        if (quizId == Guid.Empty)
        {
            throw new ArgumentException("Quiz id must not be empty.", nameof(quizId));
        }

        QuizId = quizId;
        Name = EntityGuards.Required(name, nameof(name), "Quiz name is required.");
        DeletePasswordHash = EntityGuards.Required(deletePasswordHash, nameof(deletePasswordHash), "Delete password hash is required.");
        QuizOrganizerTokenHash = EntityGuards.Required(quizOrganizerTokenHash, nameof(quizOrganizerTokenHash), "Organizer token hash is required.");
        CreatedAtUtc = EntityGuards.Utc(createdAtUtc, nameof(createdAtUtc));
        IsStartAllowedForEveryone = false;
    }

    public Guid QuizId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string DeletePasswordHash { get; private set; } = string.Empty;

    public string QuizOrganizerTokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public bool IsStartAllowedForEveryone { get; private set; }

    public IReadOnlyCollection<Question> Questions => _questions;

    public IReadOnlyCollection<QuizSession> Sessions => _sessions;

    public IReadOnlyCollection<AuditLog> AuditLogs => _auditLogs;

    public static Quiz Create(Guid quizId, string name, string deletePasswordHash, string quizOrganizerTokenHash, DateTime createdAtUtc)
    {
        return new Quiz(quizId, name, deletePasswordHash, quizOrganizerTokenHash, createdAtUtc);
    }

    public void MarkAsDeleted(DateTime deletedAtUtc)
    {
        DeletedAtUtc = EntityGuards.Utc(deletedAtUtc, nameof(deletedAtUtc));
        IsDeleted = true;
    }

    public void SetStartPermission(bool isStartAllowedForEveryone)
    {
        IsStartAllowedForEveryone = isStartAllowedForEveryone;
    }
}