using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace QuizApp.Server.Application.Sessions;

public sealed class SessionProgressionBackgroundService : BackgroundService
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SessionProgressionBackgroundService> _logger;

    public SessionProgressionBackgroundService(IServiceScopeFactory serviceScopeFactory, ILogger<SessionProgressionBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session progression background service started.");

        using var timer = new PeriodicTimer(ProgressInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var sessionParticipationService = scope.ServiceProvider.GetRequiredService<ISessionParticipationService>();
                await sessionParticipationService.ProgressDueSessionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during session progression tick.");
            }
        }

        _logger.LogInformation("Session progression background service stopped.");
    }
}
