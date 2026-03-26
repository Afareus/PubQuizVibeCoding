using Microsoft.AspNetCore.SignalR.Client;

namespace QuizApp.Client.Realtime;

public sealed class ReconnectWithinSixtySecondsPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(20)
    ];

    private static readonly TimeSpan MaxReconnectWindow = TimeSpan.FromSeconds(60);

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (retryContext.ElapsedTime >= MaxReconnectWindow)
        {
            return null;
        }

        var retryDelay = retryContext.PreviousRetryCount < RetryDelays.Length
            ? RetryDelays[retryContext.PreviousRetryCount]
            : TimeSpan.FromSeconds(20);

        return retryContext.ElapsedTime + retryDelay <= MaxReconnectWindow
            ? retryDelay
            : null;
    }
}
