using Microsoft.AspNetCore.SignalR;

namespace QuizApp.Server.Application.Sessions;

public sealed class SessionHub : Hub
{
    public async Task<bool> SubscribeToSessionAsync(Guid sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionRealtimeGroups.ForSession(sessionId));
        return true;
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
