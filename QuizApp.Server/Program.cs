using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 64 * 1024;
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

app.MapHealthChecks("/health");

app.Run();
