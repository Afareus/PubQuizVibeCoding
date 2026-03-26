using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuizApp.Server.Application.QuizImport;
using QuizApp.Server.Application.Quizzes;
using QuizApp.Server.Application.Sessions;
using QuizApp.Server.Configuration;
using QuizApp.Server.Persistence;

var builder = WebApplication.CreateBuilder(args);

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
        policy
            .SetIsOriginAllowed(static origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QuizAppDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("ClientOrigins");

app.MapHealthChecks("/health");
app.MapQuizManagementEndpoints();
app.MapSessionParticipationEndpoints();
app.MapHub<SessionHub>("/hubs/sessions");

app.Run();
