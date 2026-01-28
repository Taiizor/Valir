# API Reference

Complete API documentation for Valir.

## Valir.Abstractions

### IJobQueue

Primary interface for job queue operations.

```csharp
public interface IJobQueue
{
    /// <summary>
    /// Enqueue a single job.
    /// </summary>
    /// <param name="type">Job type identifier</param>
    /// <param name="payload">Serialized job payload</param>
    /// <param name="priority">Priority (higher = first)</param>
    /// <param name="queue">Queue name</param>
    /// <param name="delayUntil">Delay execution until</param>
    /// <param name="idempotencyKey">Deduplication key</param>
    /// <returns>Job ID</returns>
    Task<string> EnqueueAsync(
        string type,
        byte[] payload,
        int priority = 0,
        string queue = "default",
        DateTimeOffset? delayUntil = null,
        string? idempotencyKey = null);

    /// <summary>
    /// Enqueue multiple jobs in a batch.
    /// </summary>
    Task<IReadOnlyList<string>> EnqueueBatchAsync(
        IEnumerable<JobEnvelope> jobs);

    /// <summary>
    /// Claim jobs for processing.
    /// </summary>
    Task<IReadOnlyList<JobEnvelope>> ClaimAsync(
        string queue,
        int count,
        TimeSpan visibilityTimeout);

    /// <summary>
    /// Mark job as completed.
    /// </summary>
    Task CompleteAsync(string jobId);

    /// <summary>
    /// Mark job as failed.
    /// </summary>
    Task FailAsync(string jobId, string? error = null);
}
```

### JobEnvelope

Job data transfer object.

```csharp
public record JobEnvelope(
    string Id,
    string Type,
    byte[] Payload,
    string Queue,
    int Priority,
    int Attempt,
    string? IdempotencyKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DelayUntil);
```

### IEventBroker

Interface for event bus operations.

```csharp
public interface IEventBroker
{
    /// <summary>
    /// Publish an event to a topic.
    /// </summary>
    Task PublishAsync(string topic, EventEnvelope envelope);

    /// <summary>
    /// Subscribe to events on a topic.
    /// </summary>
    Task SubscribeAsync(
        string topic,
        string subscriptionId,
        Func<EventEnvelope, Task> onMessage,
        CancellationToken ct);

    /// <summary>
    /// Unsubscribe from a topic.
    /// </summary>
    Task UnsubscribeAsync(string topic, string subscriptionId);
}
```

### EventEnvelope

Event data transfer object.

```csharp
public record EventEnvelope(
    string Id,
    string Topic,
    byte[] Payload,
    DateTimeOffset PublishedAt);
```

### IDistributedLock

Distributed locking interface.

```csharp
public interface IDistributedLock
{
    /// <summary>
    /// Acquire a lock.
    /// </summary>
    /// <returns>Lock handle, or null if not acquired</returns>
    Task<IAsyncDisposable?> AcquireAsync(
        string resource,
        TimeSpan expiry,
        CancellationToken ct = default);

    /// <summary>
    /// Extend lock expiry.
    /// </summary>
    Task<bool> ExtendAsync(
        string resource,
        string token,
        TimeSpan expiry);

    /// <summary>
    /// Release a lock.
    /// </summary>
    Task ReleaseAsync(string resource, string token);
}
```

### IRateLimiter

Rate limiting interface.

```csharp
public interface IRateLimiter
{
    /// <summary>
    /// Check if action is allowed under rate limit.
    /// </summary>
    /// <param name="key">Rate limit key</param>
    /// <param name="limit">Max requests</param>
    /// <param name="window">Time window</param>
    /// <returns>True if allowed</returns>
    Task<bool> IsAllowedAsync(
        string key,
        int limit,
        TimeSpan window);

    /// <summary>
    /// Get remaining quota.
    /// </summary>
    Task<int> GetRemainingAsync(
        string key,
        int limit,
        TimeSpan window);
}
```

### IJobWorker

Job handler interface.

```csharp
public interface IJobWorker
{
    /// <summary>
    /// Job type this worker handles.
    /// </summary>
    string JobType { get; }

    /// <summary>
    /// Execute the job.
    /// </summary>
    Task ExecuteAsync(JobEnvelope job, CancellationToken ct);
}
```

## Valir.Core

### WorkerRuntime

Manages job processing lifecycle.

```csharp
public class WorkerRuntime
{
    public WorkerRuntime(
        IJobQueue queue,
        IEnumerable<IJobWorker> workers,
        ValirOptions options,
        ILogger<WorkerRuntime> logger);

    /// <summary>
    /// Start processing jobs.
    /// </summary>
    public Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Initiate graceful shutdown.
    /// </summary>
    public Task StopAsync();

    /// <summary>
    /// Number of currently processing jobs.
    /// </summary>
    public int ActiveJobs { get; }

    /// <summary>
    /// Whether in drain mode.
    /// </summary>
    public bool IsDraining { get; }
}
```

### RetryPolicy

Retry calculation with exponential backoff.

```csharp
public static class RetryPolicy
{
    /// <summary>
    /// Calculate delay for next retry.
    /// </summary>
    public static TimeSpan CalculateDelay(
        int attempt,
        TimeSpan baseDelay,
        TimeSpan? maxDelay = null);

    /// <summary>
    /// Check if should retry.
    /// </summary>
    public static bool ShouldRetry(int attempt, int maxAttempts);
}
```

### JobStateMachine

Job state transitions.

```csharp
public enum JobState
{
    Pending,
    Scheduled,
    Processing,
    Completed,
    Failed,
    DeadLetter
}

public static class JobStateMachine
{
    public static bool CanTransition(JobState from, JobState to);
    public static JobState GetNextState(JobState current, bool success);
}
```

## Valir.AspNet

### Extension Methods

```csharp
public static class ValirServiceCollectionExtensions
{
    /// <summary>
    /// Add Valir services with Redis backend.
    /// </summary>
    public static IServiceCollection AddValir(
        this IServiceCollection services,
        Action<ValirOptions>? configure = null);
}
```

### ValirTelemetry

OpenTelemetry integration.

```csharp
public static class ValirTelemetry
{
    public static readonly string SourceName = "Valir";
    public static readonly ActivitySource Source;

    public static Activity? StartJobActivity(JobEnvelope job);
    public static void RecordSuccess(Activity? activity);
    public static void RecordFailure(Activity? activity, Exception ex);
}
```

## Valir.Redis

### RedisJobQueue

Redis implementation of IJobQueue.

Lua scripts used:
- `claim_job.lua` — Atomic claim with visibility timeout
- `complete_job.lua` — Remove job and cleanup
- `fail_job.lua` — Increment attempt, reschedule or dead-letter
- `requeue_due_retries.lua` — Move delayed jobs to ready queue

### RedisDistributedLock

Redis implementation of IDistributedLock.

Uses Lua scripts for atomic acquire/release.

### RedisRateLimiter

Redis implementation of IRateLimiter.

Uses sliding window algorithm with Lua scripts.

## Valir.EntityFrameworkCore

### OutboxJobQueue

Outbox-backed job queue.

```csharp
public class OutboxJobQueue
{
    public Task<string> EnqueueAsync(
        string type,
        byte[] payload,
        int priority = 0,
        string queue = "default",
        DateTimeOffset? delayUntil = null,
        string? idempotencyKey = null);
}
```

### OutboxProcessor

Background service that processes outbox.

```csharp
public class OutboxProcessor : BackgroundService
{
    // Polls outbox table and pushes to Redis
    // Handles retries and error marking
}
```

### Model Configuration

```csharp
public static class ValirModelBuilderExtensions
{
    /// <summary>
    /// Apply Valir outbox entity configuration.
    /// </summary>
    public static ModelBuilder ApplyValirOutbox(this ModelBuilder modelBuilder);
}
```
