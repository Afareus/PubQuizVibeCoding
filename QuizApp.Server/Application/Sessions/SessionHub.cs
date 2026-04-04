using Microsoft.AspNetCore.SignalR;

namespace QuizApp.Server.Application.Sessions;

public sealed class SessionHub : Hub
{
    private readonly ILogger<SessionHub> _logger;

    public SessionHub(ILogger<SessionHub> logger)
    {
        _logger = logger;
    }

    public async Task<bool> SubscribeToSessionAsync(Guid sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionRealtimeGroups.ForSession(sessionId));
        _logger.LogDebug("SignalR subscribe. SessionId={SessionId}, ConnectionId={ConnectionId}",
            sessionId, Context.ConnectionId);
        return true;
    }

    public Task UnsubscribeFromSessionAsync(Guid sessionId)
    {
        _logger.LogDebug("SignalR unsubscribe. SessionId={SessionId}, ConnectionId={ConnectionId}",
            sessionId, Context.ConnectionId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionRealtimeGroups.ForSession(sessionId));
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("SignalR client connected. ConnectionId={ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("SignalR client disconnected. ConnectionId={ConnectionId}, HasException={HasException}",
            Context.ConnectionId, exception is not null);
        return base.OnDisconnectedAsync(exception);
    }
}

public static class SessionRealtimeGroups
{
    public static string ForSession(Guid sessionId)
    {
        return $"session:{sessionId:N}";
    }
}
