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

    public SessionRealtimePublisher(IHubContext<SessionHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishSessionEventAsync(Guid sessionId, RealtimeEventName eventName, CancellationToken cancellationToken)
    {
        return _hubContext
            .Clients
            .Group(SessionRealtimeGroups.ForSession(sessionId))
            .SendAsync(eventName.ToWireName(), cancellationToken);
    }
}
