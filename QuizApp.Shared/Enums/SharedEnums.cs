namespace QuizApp.Shared.Enums;

public enum SessionStatus
{
    Waiting = 0,
    Running = 1,
    Finished = 2,
    Cancelled = 3,
    Paused = 4
}

public enum OptionKey
{
    A = 0,
    B = 1,
    C = 2,
    D = 3
}

public enum QuestionType
{
    MultipleChoice = 0,
    NumericClosest = 1
}

public enum ParticipantPresenceStatus
{
    Connected = 0,
    TemporarilyDisconnected = 1,
    Inactive = 2
}

public enum ApiErrorCode
{
    ValidationFailed = 0,
    CsvValidationFailed = 1,
    MissingAuthToken = 2,
    InvalidAuthToken = 3,
    ResourceNotFound = 4,
    TeamNameAlreadyUsed = 5,
    QuestionClosed = 6,
    AlreadyAnswered = 7,
    SessionStateChanged = 8,
    QuizHasActiveSessions = 9,
    QuizHasNoQuestions = 10,
    RateLimited = 11,
    QuizStartLocked = 12
}

public enum RealtimeEventName
{
    SessionCreated = 0,
    TeamJoined = 1,
    SessionStarted = 2,
    QuestionChanged = 3,
    SessionFinished = 4,
    SessionCancelled = 5,
    ResultsReady = 6,
    TeamLeft = 7
}

public static class RealtimeEventNameExtensions
{
    public static string ToWireName(this RealtimeEventName eventName)
    {
        return eventName switch
        {
            RealtimeEventName.SessionCreated => "session.created",
            RealtimeEventName.TeamJoined => "team.joined",
            RealtimeEventName.SessionStarted => "session.started",
            RealtimeEventName.QuestionChanged => "question.changed",
            RealtimeEventName.SessionFinished => "session.finished",
            RealtimeEventName.SessionCancelled => "session.cancelled",
            RealtimeEventName.ResultsReady => "results.ready",
            RealtimeEventName.TeamLeft => "team.left",
            _ => throw new ArgumentOutOfRangeException(nameof(eventName), eventName, "Unknown realtime event name.")
        };
    }
}
