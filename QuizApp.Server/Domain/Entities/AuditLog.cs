namespace QuizApp.Server.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    private AuditLog(Guid auditLogId, DateTime occurredAtUtc, string actionType, Guid? quizId, Guid? sessionId, string payloadJson)
    {
        if (auditLogId == Guid.Empty)
        {
            throw new ArgumentException("Audit log id must not be empty.", nameof(auditLogId));
        }

        AuditLogId = auditLogId;
        OccurredAtUtc = EntityGuards.Utc(occurredAtUtc, nameof(occurredAtUtc));
        ActionType = EntityGuards.Required(actionType, nameof(actionType), "Action type is required.");
        QuizId = quizId;
        SessionId = sessionId;
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
    }

    public Guid AuditLogId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string ActionType { get; private set; } = string.Empty;

    public Guid? QuizId { get; private set; }

    public Guid? SessionId { get; private set; }

    public string PayloadJson { get; private set; } = "{}";

    public Quiz? Quiz { get; private set; }

    public QuizSession? Session { get; private set; }

    public static AuditLog Create(Guid auditLogId, DateTime occurredAtUtc, string actionType, Guid? quizId, Guid? sessionId, string payloadJson)
    {
        return new AuditLog(auditLogId, occurredAtUtc, actionType, quizId, sessionId, payloadJson);
    }
}