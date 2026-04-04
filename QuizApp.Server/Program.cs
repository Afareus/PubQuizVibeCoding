using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuizApp.Server.Application.QuizImport;
using QuizApp.Server.Application.Quizzes;
using QuizApp.Server.Application.Sessions;
using QuizApp.Server.Configuration;
using QuizApp.Server.Persistence;

var builder = WebApplication.CreateBuilder(args);

var configuredCorsOrigins = builder.Configuration
    .GetSection(CorsOptions.SectionName)
    .Get<CorsOptions>()?
    .AllowedOrigins
    .Where(static origin => !string.IsNullOrWhiteSpace(origin))
    .Select(static origin => origin.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

builder.Services.AddOptions<PostgreSqlOptions>()
    .Bind(builder.Configuration.GetSection(PostgreSqlOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddDbContext<QuizAppDbContext>((serviceProvider, options) =>
{
    var postgreSqlOptions = serviceProvider.GetRequiredService<IOptions<PostgreSqlOptions>>().Value;
    options.UseNpgsql(postgreSqlOptions.ConnectionString);
});

builder.Services.AddScoped<IQuizCsvParser, QuizCsvParser>();
builder.Services.AddScoped<IQuizManagementService, QuizManagementService>();
builder.Services.AddScoped<ISessionParticipationService, SessionParticipationService>();
builder.Services.AddSingleton<ISessionRealtimePublisher, SessionRealtimePublisher>();
builder.Services.AddSingleton<IReconnectMetrics, ReconnectMetrics>();
builder.Services.AddHostedService<SessionProgressionBackgroundService>();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 64 * 1024;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientOrigins", policy =>
    {
        if (configuredCorsOrigins.Length > 0)
        {
            policy.WithOrigins(configuredCorsOrigins);
        }
        else if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        {
            policy.SetIsOriginAllowed(static origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
            });
        }
        else
        {
            throw new InvalidOperationException("CORS policy 'ClientOrigins' requires at least one configured origin in production.");
        }

        policy
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("JoinPerIp", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolveRemoteIp(httpContext),
            factory: static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("SubmitPerTeam", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolveTeamKey(httpContext),
            factory: static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("OrganizerMutations", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolveOrganizerKey(httpContext),
            factory: static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("HeartbeatPerParticipant", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolveHeartbeatKey(httpContext),
            factory: static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QuizAppDbContext>();
    if (!app.Environment.IsEnvironment("Testing"))
    {
        await dbContext.Database.MigrateAsync();
    }

    var sessionParticipationService = scope.ServiceProvider.GetRequiredService<ISessionParticipationService>();
    await sessionParticipationService.TerminateNonTerminalSessionsAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("ClientOrigins");
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapQuizManagementEndpoints();
app.MapSessionParticipationEndpoints();
app.MapHub<SessionHub>("/hubs/sessions");
app.MapDiagnosticsEndpoints();

app.Run();

static string ResolveRemoteIp(HttpContext httpContext)
{
    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
}

static string ResolveTeamKey(HttpContext httpContext)
{
    if (httpContext.Request.Headers.TryGetValue("X-Team-Reconnect-Token", out var tokenValue) && !string.IsNullOrWhiteSpace(tokenValue))
    {
        return $"team:{tokenValue.ToString().Trim()}";
    }

    return $"ip:{ResolveRemoteIp(httpContext)}";
}

static string ResolveHeartbeatKey(HttpContext httpContext)
{
    if (httpContext.Request.Headers.TryGetValue("X-Team-Reconnect-Token", out var teamToken) && !string.IsNullOrWhiteSpace(teamToken))
    {
        return $"heartbeat-team:{teamToken.ToString().Trim()}";
    }

    if (httpContext.Request.Headers.TryGetValue("X-Organizer-Token", out var organizerToken) && !string.IsNullOrWhiteSpace(organizerToken))
    {
        return $"heartbeat-organizer-token:{organizerToken.ToString().Trim()}";
    }

    if (httpContext.Request.Headers.TryGetValue("X-Quiz-Password", out var organizerPassword) && !string.IsNullOrWhiteSpace(organizerPassword))
    {
        return $"heartbeat-organizer-password:{organizerPassword.ToString().Trim()}";
    }

    return $"heartbeat-ip:{ResolveRemoteIp(httpContext)}";
}

static string ResolveOrganizerKey(HttpContext httpContext)
{
    if (httpContext.Request.Headers.TryGetValue("X-Organizer-Token", out var organizerToken) && !string.IsNullOrWhiteSpace(organizerToken))
    {
        return $"organizer-token:{organizerToken.ToString().Trim()}";
    }

    if (httpContext.Request.Headers.TryGetValue("X-Quiz-Password", out var organizerPassword) && !string.IsNullOrWhiteSpace(organizerPassword))
    {
        return $"organizer-password:{organizerPassword.ToString().Trim()}";
    }

    return $"ip:{ResolveRemoteIp(httpContext)}";
}

public partial class Program;
