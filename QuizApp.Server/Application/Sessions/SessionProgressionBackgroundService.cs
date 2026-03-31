using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace QuizApp.Server.Application.Sessions;

public sealed class SessionProgressionBackgroundService : BackgroundService
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _serviceScopeFactory;

    public SessionProgressionBackgroundService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ProgressInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var sessionParticipationService = scope.ServiceProvider.GetRequiredService<ISessionParticipationService>();
            await sessionParticipationService.ProgressDueSessionsAsync(stoppingToken);
        }
    }
}
