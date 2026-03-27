using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using QuizApp.Server.Application.Sessions;
using QuizApp.Server.Persistence;
using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Tests;

public class ApiIntegrationTests
{
    [Fact]
    public async Task OrganizerPassword_AllowsCreateImportCreateSessionAndSnapshotFlow()
    {
        await using var factory = new QuizAppApiFactory();
        using var client = factory.CreateClient();

        var createQuizResponse = await CreateQuizAsync(client, "Integrační kvíz", "tajneheslo");

        var importRequest = new ImportQuizCsvRequest(
            createQuizResponse.QuizId,
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n");

        using var importMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/quizzes/{createQuizResponse.QuizId}/import-csv")
        {
            Content = JsonContent.Create(importRequest)
        };
        importMessage.Headers.Add("X-Quiz-Password", "tajneheslo");

        using var importResponse = await client.SendAsync(importMessage);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

        using var createSessionMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/quizzes/{createQuizResponse.QuizId}/sessions");
        createSessionMessage.Content = JsonContent.Create(new CreateSessionRequest("ABCD2345"));
        createSessionMessage.Headers.Add("X-Quiz-Password", "tajneheslo");

        using var createSessionResponse = await client.SendAsync(createSessionMessage);
        Assert.Equal(HttpStatusCode.Created, createSessionResponse.StatusCode);

        var sessionPayload = await createSessionResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.NotNull(sessionPayload);
        Assert.Equal(SessionStatus.Waiting, sessionPayload!.Status);

        var joinResponse = await client.PostAsJsonAsync("/api/sessions/join", new JoinSessionRequest(sessionPayload.JoinCode, "Tým Integrace"));
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);

        var joinPayload = await joinResponse.Content.ReadFromJsonAsync<JoinSessionResponse>();
        Assert.NotNull(joinPayload);

        using var organizerSnapshotMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/sessions/{sessionPayload.SessionId}");
        organizerSnapshotMessage.Headers.Add("X-Quiz-Password", "tajneheslo");

        using var organizerSnapshotResponse = await client.SendAsync(organizerSnapshotMessage);
        Assert.Equal(HttpStatusCode.OK, organizerSnapshotResponse.StatusCode);

        var snapshot = await organizerSnapshotResponse.Content.ReadFromJsonAsync<OrganizerSessionSnapshotResponse>();
        Assert.NotNull(snapshot);
        Assert.Equal(sessionPayload.SessionId, snapshot!.SessionId);
        Assert.Contains(snapshot.Teams, team => team.TeamId == joinPayload!.TeamId && team.TeamName == "Tým Integrace");
    }

    [Fact]
    public async Task TeamStateEndpoint_RequiresReconnectToken()
    {
        await using var factory = new QuizAppApiFactory();
        using var client = factory.CreateClient();

        var createQuizResponse = await CreateQuizAsync(client, "Integrační token kvíz", "heslo");
        await ImportSingleQuestionAsync(client, createQuizResponse.QuizId, "heslo");

        var session = await CreateSessionAsync(client, createQuizResponse.QuizId, "heslo", "EFGH2345");
        var joinResponse = await client.PostAsJsonAsync("/api/sessions/join", new JoinSessionRequest(session.JoinCode, "Tým Token"));
        var joinPayload = await joinResponse.Content.ReadFromJsonAsync<JoinSessionResponse>();
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        Assert.NotNull(joinPayload);

        using var missingTokenMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/sessions/{session.SessionId}/state?teamId={joinPayload!.TeamId}");
        using var missingTokenResponse = await client.SendAsync(missingTokenMessage);
        Assert.Equal(HttpStatusCode.Unauthorized, missingTokenResponse.StatusCode);

        using var invalidTokenMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/sessions/{session.SessionId}/state?teamId={joinPayload.TeamId}");
        invalidTokenMessage.Headers.Add("X-Team-Reconnect-Token", "invalid-token");
        using var invalidTokenResponse = await client.SendAsync(invalidTokenMessage);
        Assert.Equal(HttpStatusCode.Forbidden, invalidTokenResponse.StatusCode);

        using var validTokenMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/sessions/{session.SessionId}/state?teamId={joinPayload.TeamId}");
        validTokenMessage.Headers.Add("X-Team-Reconnect-Token", joinPayload.TeamReconnectToken);
        using var validTokenResponse = await client.SendAsync(validTokenMessage);
        Assert.Equal(HttpStatusCode.OK, validTokenResponse.StatusCode);
    }

    private static async Task<CreateQuizResponse> CreateQuizAsync(HttpClient client, string name, string password)
    {
        var createResponse = await client.PostAsJsonAsync("/api/quizzes", new CreateQuizRequest(name, password));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateQuizResponse>();
        Assert.NotNull(createPayload);
        return createPayload!;
    }

    private static async Task ImportSingleQuestionAsync(HttpClient client, Guid quizId, string password)
    {
        var request = new ImportQuizCsvRequest(
            quizId,
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n");

        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/quizzes/{quizId}/import-csv")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Quiz-Password", password);

        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<CreateSessionResponse> CreateSessionAsync(HttpClient client, Guid quizId, string password, string joinCode)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/quizzes/{quizId}/sessions");
        message.Content = JsonContent.Create(new CreateSessionRequest(joinCode));
        message.Headers.Add("X-Quiz-Password", password);

        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private sealed class QuizAppApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"api-integration-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PostgreSql:ConnectionString"] = "Host=localhost;Database=quizapp-tests;Username=test;Password=test"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<QuizAppDbContext>>();
                services.RemoveAll<QuizAppDbContext>();

                var progressionWorker = services.FirstOrDefault(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType == typeof(SessionProgressionBackgroundService));

                if (progressionWorker is not null)
                {
                    services.Remove(progressionWorker);
                }

                services.AddDbContext<QuizAppDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}
