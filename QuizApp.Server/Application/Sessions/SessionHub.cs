using Microsoft.AspNetCore.SignalR;

namespace QuizApp.Server.Application.Sessions;

public sealed class SessionHub : Hub
{
    public Task SubscribeToSessionAsync(Guid sessionId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, SessionRealtimeGroups.ForSession(sessionId));
    }

    public Task UnsubscribeFromSessionAsync(Guid sessionId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionRealtimeGroups.ForSession(sessionId));
    }
}

public static class SessionRealtimeGroups
{
    public static string ForSession(Guid sessionId)
    {
        return $"session:{sessionId:N}";
    }
}
