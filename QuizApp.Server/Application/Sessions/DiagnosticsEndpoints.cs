namespace QuizApp.Server.Application.Sessions;

public static class DiagnosticsEndpoints
{
    public static void MapDiagnosticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/diagnostics");

        group.MapGet("/reconnect-metrics", (IReconnectMetrics metrics) =>
        {
            return Results.Ok(metrics.GetSnapshot());
        });

        group.MapPost("/reconnect-metrics/reset", (IReconnectMetrics metrics) =>
        {
            metrics.Reset();
            return Results.NoContent();
        });
    }
}
