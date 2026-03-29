using System.Text.Json;
using Microsoft.JSInterop;
using QuizApp.Shared.Enums;

namespace QuizApp.Client.Team;

public sealed class TeamSessionLocalStore
{
    private const string IdentityStorageKey = "quizapp.team.identities";
    private const string AnswersStorageKey = "quizapp.team.answers";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _jsRuntime;

    public TeamSessionLocalStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<StoredTeamIdentity?> FindIdentityAsync(Guid sessionId, Guid? teamId = null)
    {
        var identities = await GetIdentitiesAsync();
        if (teamId.HasValue)
        {
            return identities.FirstOrDefault(identity => identity.SessionId == sessionId && identity.TeamId == teamId.Value);
        }

        return identities.LastOrDefault(identity => identity.SessionId == sessionId);
    }

    public async Task UpsertIdentityAsync(StoredTeamIdentity identity)
    {
        var identities = (await GetIdentitiesAsync()).ToList();
        identities.RemoveAll(item => item.TeamId == identity.TeamId);
        identities.Add(identity);

        await SaveAsync(IdentityStorageKey, identities);
    }

    public async Task RemoveIdentityAsync(Guid sessionId, Guid teamId)
    {
        var identities = (await GetIdentitiesAsync()).ToList();
        identities.RemoveAll(identity => identity.SessionId == sessionId && identity.TeamId == teamId);

        var answers = (await GetAnswersAsync()).ToList();
        answers.RemoveAll(answer => answer.SessionId == sessionId && answer.TeamId == teamId);

        await SaveAsync(IdentityStorageKey, identities);
        await SaveAsync(AnswersStorageKey, answers);
    }

    public async Task<OptionKey?> FindSubmittedAnswerAsync(Guid sessionId, Guid teamId, Guid questionId)
    {
        var answers = await GetAnswersAsync();
        var submitted = answers.FirstOrDefault(answer => answer.SessionId == sessionId && answer.TeamId == teamId && answer.QuestionId == questionId);
        return submitted?.SelectedOption;
    }

    public async Task SaveSubmittedAnswerAsync(Guid sessionId, Guid teamId, Guid questionId, OptionKey selectedOption)
    {
        var answers = (await GetAnswersAsync()).ToList();
        answers.RemoveAll(answer => answer.SessionId == sessionId && answer.TeamId == teamId && answer.QuestionId == questionId);
        answers.Add(new StoredTeamAnswer(sessionId, teamId, questionId, selectedOption, DateTimeOffset.UtcNow));

        await SaveAsync(AnswersStorageKey, answers);
    }

    private async Task<IReadOnlyList<StoredTeamIdentity>> GetIdentitiesAsync()
    {
        return await ReadListAsync<StoredTeamIdentity>(IdentityStorageKey);
    }

    private async Task<IReadOnlyList<StoredTeamAnswer>> GetAnswersAsync()
    {
        return await ReadListAsync<StoredTeamAnswer>(AnswersStorageKey);
    }

    private async Task<IReadOnlyList<TItem>> ReadListAsync<TItem>(string key)
    {
        var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<TItem>>(json, SerializerOptions);
            return items ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task SaveAsync<TItem>(string key, List<TItem> items)
    {
        var json = JsonSerializer.Serialize(items, SerializerOptions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
    }
}

public sealed record StoredTeamIdentity(
    Guid SessionId,
    Guid TeamId,
    string TeamName,
    string TeamReconnectToken);

public sealed record StoredTeamAnswer(
    Guid SessionId,
    Guid TeamId,
    Guid QuestionId,
    OptionKey SelectedOption,
    DateTimeOffset SubmittedAtUtc);
