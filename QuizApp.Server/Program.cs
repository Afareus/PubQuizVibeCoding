using QuizApp.Server.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<PostgreSqlOptions>()
    .Bind(builder.Configuration.GetSection(PostgreSqlOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 64 * 1024;
});

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.Run();
