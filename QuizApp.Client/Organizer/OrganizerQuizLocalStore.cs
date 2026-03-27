using System.Text.Json;
using Microsoft.JSInterop;

namespace QuizApp.Client.Organizer;

public sealed class OrganizerQuizLocalStore
{
    private const string StorageKey = "quizapp.organizer.quizzes";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _jsRuntime;

    public OrganizerQuizLocalStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<IReadOnlyList<StoredOrganizerQuiz>> GetAllAsync()
    {
        var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<StoredOrganizerQuiz>>(json, SerializerOptions);
            return items ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<StoredOrganizerQuiz?> FindByQuizIdAsync(Guid quizId)
    {
        var items = await GetAllAsync();
        return items.FirstOrDefault(item => item.QuizId == quizId);
    }

    public async Task UpsertAsync(Guid quizId, string organizerToken, string? quizName, DateTimeOffset? createdAtUtc)
    {
        var items = (await GetAllAsync()).ToList();
        items.RemoveAll(item => item.QuizId == quizId);
        items.Add(new StoredOrganizerQuiz(quizId, organizerToken, quizName, createdAtUtc));

        var json = JsonSerializer.Serialize(items, SerializerOptions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task RemoveAsync(Guid quizId)
    {
        var items = (await GetAllAsync()).Where(item => item.QuizId != quizId).ToList();
        var json = JsonSerializer.Serialize(items, SerializerOptions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}

public sealed record StoredOrganizerQuiz(
    Guid QuizId,
    string QuizOrganizerToken,
    string? QuizName = null,
    DateTimeOffset? CreatedAtUtc = null);
