namespace QuizApp.Server.Domain.Entities;

public sealed class Team
{
    private readonly List<TeamAnswer> _answers = [];

    private Team()
    {
    }

    private Team(Guid teamId, Guid sessionId, string name, string teamReconnectTokenHash, DateTime joinedAtUtc)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Team id must not be empty.", nameof(teamId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session id must not be empty.", nameof(sessionId));
        }

        TeamId = teamId;
        SessionId = sessionId;
        Name = EntityGuards.Required(name, nameof(name), "Team name is required.");
        NormalizedTeamName = Name.ToUpperInvariant();
        TeamReconnectTokenHash = EntityGuards.Required(teamReconnectTokenHash, nameof(teamReconnectTokenHash), "Team reconnect token hash is required.");
        JoinedAtUtc = EntityGuards.Utc(joinedAtUtc, nameof(joinedAtUtc));
        LastSeenAtUtc = JoinedAtUtc;
        Status = TeamStatus.Connected;
    }

    public Guid TeamId { get; private set; }

    public Guid SessionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedTeamName { get; private set; } = string.Empty;

    public DateTime JoinedAtUtc { get; private set; }

    public DateTime LastSeenAtUtc { get; private set; }

    public string TeamReconnectTokenHash { get; private set; } = string.Empty;

    public TeamStatus Status { get; private set; }

    public QuizSession? Session { get; private set; }

    public SessionResult? SessionResult { get; private set; }

    public IReadOnlyCollection<TeamAnswer> Answers => _answers;

    public static Team Create(Guid teamId, Guid sessionId, string name, string teamReconnectTokenHash, DateTime joinedAtUtc)
    {
        return new Team(teamId, sessionId, name, teamReconnectTokenHash, joinedAtUtc);
    }

    public void MarkSeen(DateTime seenAtUtc)
    {
        LastSeenAtUtc = EntityGuards.Utc(seenAtUtc, nameof(seenAtUtc));
        Status = TeamStatus.Connected;
    }

    public void MarkDisconnected()
    {
        Status = TeamStatus.Disconnected;
    }
}