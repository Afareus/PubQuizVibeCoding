using Microsoft.AspNetCore.SignalR;
using QuizApp.Shared.Enums;

namespace QuizApp.Server.Application.Sessions;

public interface ISessionRealtimePublisher
{
    Task PublishSessionEventAsync(Guid sessionId, RealtimeEventName eventName, CancellationToken cancellationToken);
}

public sealed class SessionRealtimePublisher : ISessionRealtimePublisher
{
    private readonly IHubContext<SessionHub> _hubContext;
    private readonly ILogger<SessionRealtimePublisher> _logger;

    public SessionRealtimePublisher(IHubContext<SessionHub> hubContext, ILogger<SessionRealtimePublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task PublishSessionEventAsync(Guid sessionId, RealtimeEventName eventName, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Publishing realtime event. SessionId={SessionId}, EventName={EventName}",
            sessionId, eventName.ToWireName());

        return _hubContext
            .Clients
            .Group(SessionRealtimeGroups.ForSession(sessionId))
            .SendAsync(eventName.ToWireName(), cancellationToken);
    }
}
