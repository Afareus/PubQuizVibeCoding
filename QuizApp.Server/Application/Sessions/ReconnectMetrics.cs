using System.Collections.Concurrent;

namespace QuizApp.Server.Application.Sessions;

/// <summary>
/// In-memory singleton metrics for reconnect observability.
/// Thread-safe counters track reconnect attempts, resync durations, duplicate submits and failures.
/// 
/// Alert thresholds (recommended):
///   - FailedResyncCount > 50 in 5 min  → possible auth/token regression
///   - TeamReconnectCount > 200 in 5 min → unstable network or SignalR layer
///   - DuplicateSubmitRetryCount > 30 in 5 min → submit idempotency under heavy retry pressure
///   - AverageResyncDurationMs > 5000     → slow snapshot resolution, check DB latency
/// </summary>
public interface IReconnectMetrics
{
    void RecordTeamReconnect(Guid sessionId, Guid teamId);
    void RecordOrganizerReconnect(Guid sessionId);
    void RecordSnapshotServed(Guid sessionId, string participantType);
    void RecordDuplicateSubmitRetry(Guid sessionId, Guid teamId, Guid questionId);
    void RecordFailedResync(Guid sessionId, string reason);
    void RecordResyncDuration(Guid sessionId, long durationMs);
    ReconnectMetricsSnapshot GetSnapshot();
    void Reset();
}

public sealed class ReconnectMetrics : IReconnectMetrics
{
    private long _teamReconnectCount;
    private long _organizerReconnectCount;
    private long _snapshotServedCount;
    private long _duplicateSubmitRetryCount;
    private long _failedResyncCount;
    private long _resyncDurationTotalMs;
    private long _resyncDurationSampleCount;
    private DateTimeOffset _metricsStartedAtUtc = DateTimeOffset.UtcNow;

    private readonly ConcurrentQueue<ReconnectEvent> _recentEvents = new();
    private const int MaxRecentEvents = 200;

    public void RecordTeamReconnect(Guid sessionId, Guid teamId)
    {
        Interlocked.Increment(ref _teamReconnectCount);
        EnqueueEvent(new ReconnectEvent(DateTimeOffset.UtcNow, "TeamReconnect", sessionId, teamId, null));
    }

    public void RecordOrganizerReconnect(Guid sessionId)
    {
        Interlocked.Increment(ref _organizerReconnectCount);
        EnqueueEvent(new ReconnectEvent(DateTimeOffset.UtcNow, "OrganizerReconnect", sessionId, null, null));
    }

    public void RecordSnapshotServed(Guid sessionId, string participantType)
    {
        Interlocked.Increment(ref _snapshotServedCount);
    }

    public void RecordDuplicateSubmitRetry(Guid sessionId, Guid teamId, Guid questionId)
    {
        Interlocked.Increment(ref _duplicateSubmitRetryCount);
        EnqueueEvent(new ReconnectEvent(DateTimeOffset.UtcNow, "DuplicateSubmitRetry", sessionId, teamId, questionId));
    }

    public void RecordFailedResync(Guid sessionId, string reason)
    {
        Interlocked.Increment(ref _failedResyncCount);
        EnqueueEvent(new ReconnectEvent(DateTimeOffset.UtcNow, $"FailedResync:{reason}", sessionId, null, null));
    }

    public void RecordResyncDuration(Guid sessionId, long durationMs)
    {
        Interlocked.Add(ref _resyncDurationTotalMs, durationMs);
        Interlocked.Increment(ref _resyncDurationSampleCount);
    }

    public ReconnectMetricsSnapshot GetSnapshot()
    {
        var sampleCount = Interlocked.Read(ref _resyncDurationSampleCount);
        var totalMs = Interlocked.Read(ref _resyncDurationTotalMs);
        var averageResyncMs = sampleCount > 0 ? (double)totalMs / sampleCount : 0d;

        return new ReconnectMetricsSnapshot(
            Interlocked.Read(ref _teamReconnectCount),
            Interlocked.Read(ref _organizerReconnectCount),
            Interlocked.Read(ref _snapshotServedCount),
            Interlocked.Read(ref _duplicateSubmitRetryCount),
            Interlocked.Read(ref _failedResyncCount),
            Math.Round(averageResyncMs, 2),
            sampleCount,
            _metricsStartedAtUtc,
            DateTimeOffset.UtcNow,
            _recentEvents.ToArray());
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _teamReconnectCount, 0);
        Interlocked.Exchange(ref _organizerReconnectCount, 0);
        Interlocked.Exchange(ref _snapshotServedCount, 0);
        Interlocked.Exchange(ref _duplicateSubmitRetryCount, 0);
        Interlocked.Exchange(ref _failedResyncCount, 0);
        Interlocked.Exchange(ref _resyncDurationTotalMs, 0);
        Interlocked.Exchange(ref _resyncDurationSampleCount, 0);
        _metricsStartedAtUtc = DateTimeOffset.UtcNow;

        while (_recentEvents.TryDequeue(out _)) { }
    }

    private void EnqueueEvent(ReconnectEvent reconnectEvent)
    {
        _recentEvents.Enqueue(reconnectEvent);
        while (_recentEvents.Count > MaxRecentEvents)
        {
            _recentEvents.TryDequeue(out _);
        }
    }
}

public sealed record ReconnectMetricsSnapshot(
    long TeamReconnectCount,
    long OrganizerReconnectCount,
    long SnapshotServedCount,
    long DuplicateSubmitRetryCount,
    long FailedResyncCount,
    double AverageResyncDurationMs,
    long ResyncDurationSampleCount,
    DateTimeOffset MetricsStartedAtUtc,
    DateTimeOffset SnapshotTakenAtUtc,
    ReconnectEvent[] RecentEvents);

public sealed record ReconnectEvent(
    DateTimeOffset OccurredAtUtc,
    string EventType,
    Guid SessionId,
    Guid? TeamId,
    Guid? QuestionId);
