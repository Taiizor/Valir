# API Reference

Complete API documentation for Valir.

## Valir.Abstractions

### IJobQueue

Primary interface for job queue operations.

```csharp
public interface IJobQueue
{
    /// <summary>
    /// Enqueue a single job with optional delay and priority.
    /// </summary>
    /// <param name="type">Job type name for handler routing.</param>
    /// <param name="payload">Serialized job payload.</param>
    /// <param name="delay">Optional delay before job becomes visible.</param>
    /// <param name="priority">Job priority (0 = default, higher = faster).</param>
    /// <param name="idempotencyKey">Optional key for deduplication.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The generated job ID.</returns>
    Task<string> EnqueueAsync(
        string type,
        byte[] payload,
        TimeSpan? delay = null,
        int priority = 0,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    /// <summary>
    /// Batch enqueue multiple jobs for high-throughput scenarios.
    /// Uses Redis pipelining for performance (50x-100x faster than individual calls).
    /// </summary>
    /// <param name="jobs">Collection of job definitions.</param>
    /// <param name="priority">Priority for all jobs in the batch.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Array of generated job IDs.</returns>
    Task<string[]> EnqueueBatchAsync(
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs,
        int priority = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Claim the next available job for processing.
    /// </summary>
    /// <param name="workerId">Unique identifier of the claiming worker.</param>
    /// <param name="claimTimeout">Duration for which the job is locked.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Job envelope or null if no jobs available.</returns>
    Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout, CancellationToken ct = default);

    /// <summary>
    /// Mark a job as successfully completed.
    /// </summary>
    /// <param name="jobId">The job to complete.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    Task CompleteAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Mark a job as failed, scheduling retry or dead-letter.
    /// </summary>
    /// <param name="jobId">The job that failed.</param>
    /// <param name="reason">Failure reason for logging.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    Task FailAsync(string jobId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Release a job back to the queue (e.g., during graceful shutdown).
    /// </summary>
    /// <param name="jobId">The job to release.</param>
    /// <param name="delay">Optional delay before the job becomes visible again.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    Task ReleaseAsync(string jobId, TimeSpan? delay = null, CancellationToken ct = default);

    /// <summary>
    /// Atomically extend the lock TTL for a job currently being processed.
    /// </summary>
    /// <param name="jobId">The job to extend the lock for.</param>
    /// <param name="workerId">Unique identifier of the worker holding the lock.</param>
    /// <param name="extension">Duration to extend the lock by.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>True if the lock was extended successfully.</returns>
    Task<bool> ExtendLockAsync(string jobId, string workerId, TimeSpan extension, CancellationToken ct = default);
}
```

### JobEnvelope

Job data transfer object containing all metadata for a background job.

```csharp
public sealed record JobEnvelope(
    string Id,
    string Type,
    byte[] Payload,
    int Attempts,
    int MaxAttempts,
    DateTimeOffset CreatedAt,
    TimeSpan VisibilityTimeout,
    string? IdempotencyKey = null,
    int Priority = 0
);
```

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique identifier for this job instance |
| `Type` | `string` | The job type name used for handler routing |
| `Payload` | `byte[]` | Serialized job payload as bytes |
| `Attempts` | `int` | Current attempt count (starts at 0) |
| `MaxAttempts` | `int` | Maximum retry attempts before dead-lettering |
| `CreatedAt` | `DateTimeOffset` | Timestamp when the job was enqueued |
| `VisibilityTimeout` | `TimeSpan` | Duration for which the job is invisible after being claimed |
| `IdempotencyKey` | `string?` | Optional key for at-least-once deduplication |
| `Priority` | `int` | Job priority (0 = default, higher = faster processing) |

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

Distributed lock for coordinating access across workers.

```csharp
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    /// The resource key being locked.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// The owner identifier (typically worker ID).
    /// </summary>
    string Owner { get; }

    /// <summary>
    /// Attempt to acquire the lock with specified TTL.
    /// </summary>
    /// <param name="ttl">Time-to-live for the lock.</param>
    /// <returns>True if lock was acquired, false if already held.</returns>
    Task<bool> AcquireAsync(TimeSpan ttl);

    /// <summary>
    /// Extend the lock TTL (must be current owner).
    /// </summary>
    /// <param name="ttl">New TTL duration.</param>
    /// <returns>True if extended, false if ownership lost.</returns>
    Task<bool> ExtendAsync(TimeSpan ttl);

    /// <summary>
    /// Release the lock (must be current owner).
    /// </summary>
    Task ReleaseAsync();
}
```

### IRateLimiter

Rate limiter for throttling operations. Implements sliding window algorithm.

```csharp
public interface IRateLimiter
{
    /// <summary>
    /// Check if an operation is allowed under the rate limit.
    /// </summary>
    /// <param name="key">Rate limit key (e.g., user ID, API key).</param>
    /// <param name="max">Maximum allowed operations in the window.</param>
    /// <param name="window">Time window for the limit.</param>
    /// <returns>True if allowed, false if rate limited.</returns>
    Task<bool> AllowAsync(string key, int max, TimeSpan window);
}
```

### IJobWorker

Interface for background job worker lifecycle.

```csharp
public interface IJobWorker
{
    /// <summary>
    /// Unique identifier for this worker instance.
    /// </summary>
    string WorkerId { get; }

    /// <summary>
    /// Start the worker processing loop.
    /// </summary>
    /// <param name="ct">Cancellation token for graceful shutdown.</param>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Stop the worker, draining current jobs before shutdown.
    /// </summary>
    /// <param name="ct">Cancellation token with shutdown timeout.</param>
    Task StopAsync(CancellationToken ct);
}
```

### IJobHandler<TJob>

Handler contract for processing jobs of a specific type. Handlers must be idempotent for at-least-once delivery guarantees.

```csharp
public interface IJobHandler<TJob>
{
    /// <summary>
    /// Process the job. Must be idempotent.
    /// Use JobContext.LockOwnerToken as a fencing token for external writes.
    /// </summary>
    /// <param name="job">Deserialized job payload.</param>
    /// <param name="context">Execution context with metadata.</param>
    Task HandleAsync(TJob job, JobContext context);
}
```

## Valir.Core

### WorkerRuntime

Worker runtime using System.Threading.Channels for backpressure. Implements graceful shutdown (drain mode) and adaptive polling.

```csharp
public sealed class WorkerRuntime : IJobWorker, IAsyncDisposable
{
    /// <summary>
    /// Initializes a new instance of the WorkerRuntime.
    /// </summary>
    public WorkerRuntime(
        IJobQueue queue,
        Func<JobEnvelope, JobContext, Task> handler,
        ValirOptions options,
        string? workerId = null,
        ILogger<WorkerRuntime>? logger = null,
        IValirMetrics? metrics = null);

    /// <summary>
    /// Unique identifier for this worker instance.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Start processing jobs.
    /// </summary>
    public Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Initiate graceful shutdown.
    /// </summary>
    public Task StopAsync(CancellationToken ct);

    /// <summary>
    /// Dispose the runtime and release resources.
    /// </summary>
    public ValueTask DisposeAsync();
}
```

### RetryPolicy

Calculates retry delays using exponential backoff with jitter.

```csharp
public static class RetryPolicy
{
    /// <summary>
    /// Calculate the delay for the next retry attempt.
    /// Uses exponential backoff: baseDelay * 2^attempt + random jitter.
    /// </summary>
    /// <param name="attempt">Current attempt number (0-based).</param>
    /// <param name="baseDelay">Base delay duration.</param>
    /// <param name="maxDelay">Maximum delay cap.</param>
    /// <returns>Delay before next retry.</returns>
    public static TimeSpan CalculateDelay(int attempt, TimeSpan baseDelay, TimeSpan? maxDelay = null);
}
```

Formula: `baseDelay × 2^attempt + random(0, baseDelay × 0.25)`

Example delays with 10s base:
- Attempt 1: 10-12.5s
- Attempt 2: 20-22.5s
- Attempt 3: 40-42.5s

## Valir.AspNet

### Extension Methods

```csharp
public static class ValirServiceCollectionExtensions
{
    /// <summary>
    /// Add Valir services to the DI container.
    /// </summary>
    public static IServiceCollection AddValir(
        this IServiceCollection services,
        Action<ValirOptions>? configure = null);

    /// <summary>
    /// Add Valir job queue only (for producer applications).
    /// </summary>
    public static IServiceCollection AddValirQueue(
        this IServiceCollection services,
        string redisConnectionString,
        Action<ConfigurationOptions>? configureOptions = null);

    /// <summary>
    /// Add Valir with custom Redis configuration options.
    /// </summary>
    public static IServiceCollection AddValirWithRedisOptions(
        this IServiceCollection services,
        Action<ConfigurationOptions> configureOptions,
        Action<ValirOptions>? configureValir = null);

    /// <summary>
    /// Add Valir health checks to the DI container.
    /// </summary>
    public static IHealthChecksBuilder AddValirHealthChecks(
        this IHealthChecksBuilder builder,
        string[]? tags = null);
}
```

### ValirTelemetry

OpenTelemetry integration.

```csharp
public static class ValirTelemetry
{
    public static readonly string SourceName = "Valir";
    public static readonly ActivitySource Source;

    /// <summary>
    /// Start an activity for enqueueing a job.
    /// </summary>
    public static Activity? StartEnqueue(string jobType, string? jobId = null);

    /// <summary>
    /// Start an activity for publishing an event.
    /// </summary>
    public static Activity? StartPublish(string topic, string? eventId = null);

    /// <summary>
    /// Start an activity for claiming a job from the queue.
    /// </summary>
    public static Activity? StartClaim(string workerId);

    /// <summary>
    /// Start an activity for executing a job.
    /// </summary>
    public static Activity? StartExecute(string jobId, string jobType, string workerId);

    /// <summary>
    /// Start an activity for completing a job.
    /// </summary>
    public static Activity? StartComplete(string jobId);

    /// <summary>
    /// Record an exception on an activity.
    /// </summary>
    public static void RecordException(Activity? activity, Exception ex);
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

### OutboxJobQueue<TContext>

Outbox-backed job queue that writes jobs to the database first. Ensures atomicity with application transactions.

```csharp
public class OutboxJobQueue<TContext> : IJobQueue where TContext : DbContext
{
    /// <summary>
    /// Create an outbox queue that writes to DB only.
    /// Jobs are pushed to Redis by the OutboxProcessor background service.
    /// </summary>
    public OutboxJobQueue(TContext context);

    /// <summary>
    /// Create an outbox queue with a fallback inner queue.
    /// </summary>
    public OutboxJobQueue(TContext context, IJobQueue innerQueue);

    /// <inheritdoc />
    public Task<string> EnqueueAsync(
        string type,
        byte[] payload,
        TimeSpan? delay = null,
        int priority = 0,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public Task<string[]> EnqueueBatchAsync(
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs,
        int priority = 0,
        CancellationToken ct = default);

    // These operations delegate to the inner queue (Redis)
    public Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout, CancellationToken ct = default);
    public Task CompleteAsync(string jobId, CancellationToken ct = default);
    public Task FailAsync(string jobId, string reason, CancellationToken ct = default);
    public Task ReleaseAsync(string jobId, TimeSpan? delay = null, CancellationToken ct = default);
    public Task<bool> ExtendLockAsync(string jobId, string workerId, TimeSpan extension, CancellationToken ct = default);
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
