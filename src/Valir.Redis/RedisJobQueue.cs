using StackExchange.Redis;
using Valir.Abstractions;
using Valir.Core;

namespace Valir.Redis;

/// <summary>
/// Redis-backed implementation of IJobQueue using Lua scripts for atomicity.
/// </summary>
public sealed class RedisJobQueue : IJobQueue
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ValirOptions _options;
    private readonly LuaScripts _scripts;

    // Key names
    private readonly string _waitingKey;
    private readonly string _activeKey;
    private readonly string _retryKey;
    private readonly string _deadKey;
    private readonly string _jobHashPrefix;
    private readonly string _lockKeyPrefix;
    private readonly string _payloadKeyPrefix;

    /// <summary>
    /// Initializes a new instance of the RedisJobQueue.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="options">Configuration options.</param>
    public RedisJobQueue(IConnectionMultiplexer redis, ValirOptions options)
    {
        _redis = redis;
        _options = options;
        _scripts = new LuaScripts();

        string prefix = options.KeyPrefix;
        _waitingKey = $"{prefix}queue:waiting";
        _activeKey = $"{prefix}queue:active";
        _retryKey = $"{prefix}retry";
        _deadKey = $"{prefix}dead";
        _jobHashPrefix = $"{prefix}job:";
        _lockKeyPrefix = $"{prefix}lock:";
        _payloadKeyPrefix = $"{prefix}payload:";
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync(
        string type,
        byte[] payload,
        TimeSpan? delay = null,
        int priority = 0,
        string? idempotencyKey = null)
    {
        IDatabase db = _redis.GetDatabase();
        string jobId = Guid.CreateVersion7().ToString("N");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Calculate score: lower score = higher priority + earlier time
        // Priority is negated so higher priority gets lower score
        long score = (priority * -1_000_000_000L) + now + (long)(delay?.TotalMilliseconds ?? 0);

        string jobKey = _jobHashPrefix + jobId;

        // Store job metadata
        HashEntry[] hashEntries =
        [
            new("type", type),
            new("payload", Convert.ToBase64String(payload)),
            new("attempts", 0),
            new("maxAttempts", _options.DefaultMaxAttempts),
            new("createdAt", now),
            new("priority", priority),
            new("idempotencyKey", idempotencyKey ?? "")
        ];

        await db.HashSetAsync(jobKey, hashEntries);
        await db.SortedSetAddAsync(_waitingKey, jobId, score);

        return jobId;
    }

    /// <inheritdoc />
    public async Task<string[]> EnqueueBatchAsync(
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs,
        int priority = 0)
    {
        IDatabase db = _redis.GetDatabase();
        IBatch batch = db.CreateBatch();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long score = (priority * -1_000_000_000L) + now;

        List<string> jobIds = [];
        List<Task> tasks = [];

        foreach ((string? type, byte[]? payload, string? idempotencyKey) in jobs)
        {
            string jobId = Guid.CreateVersion7().ToString("N");
            jobIds.Add(jobId);

            string jobKey = _jobHashPrefix + jobId;
            HashEntry[] hashEntries =
            [
                new("type", type),
                new("payload", Convert.ToBase64String(payload)),
                new("attempts", 0),
                new("maxAttempts", _options.DefaultMaxAttempts),
                new("createdAt", now),
                new("priority", priority),
                new("idempotencyKey", idempotencyKey ?? "")
            ];

            tasks.Add(batch.HashSetAsync(jobKey, hashEntries));
            tasks.Add(batch.SortedSetAddAsync(_waitingKey, jobId, score));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        return [.. jobIds];
    }

    /// <inheritdoc />
    public async Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout)
    {
        IDatabase db = _redis.GetDatabase();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        RedisResult result = await db.ScriptEvaluateAsync(
            _scripts.ClaimJob,
            [_waitingKey, _activeKey],
            [
                _jobHashPrefix,
                _lockKeyPrefix,
                workerId,
                now,
                (long)claimTimeout.TotalMilliseconds
            ]
        );

        if (result.IsNull)
        {
            return null;
        }

        RedisResult[] values = (RedisResult[])result!;
        string jobId = (string)values[0]!;
        string type = (string)values[1]!;
        string payloadBase64 = (string)values[2]!;
        int attempts = (int)values[3];
        int maxAttempts = (int)values[4];

        return new JobEnvelope(
            Id: jobId,
            Type: type,
            Payload: Convert.FromBase64String(payloadBase64),
            Attempts: attempts,
            MaxAttempts: maxAttempts,
            CreatedAt: DateTimeOffset.UtcNow,
            VisibilityTimeout: claimTimeout
        );
    }

    /// <inheritdoc />
    public async Task CompleteAsync(string jobId)
    {
        IDatabase db = _redis.GetDatabase();

        // For completion, we need the worker ID but we don't have it here
        // In a real implementation, we'd track this or pass it through
        await db.ScriptEvaluateAsync(
            _scripts.CompleteJob,
            [_activeKey],
            [
                _jobHashPrefix,
                _lockKeyPrefix,
                _payloadKeyPrefix,
                jobId,
                "*" // Allow any owner for now
            ]
        );
    }

    /// <inheritdoc />
    public async Task FailAsync(string jobId, string reason)
    {
        IDatabase db = _redis.GetDatabase();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Get current attempts for backoff calculation
        string jobKey = _jobHashPrefix + jobId;
        int attempts = (int)await db.HashGetAsync(jobKey, "attempts");
        TimeSpan retryDelay = RetryPolicy.CalculateDelay(attempts, _options.RetryBaseDelay);

        await db.ScriptEvaluateAsync(
            _scripts.FailJob,
            [_activeKey, _retryKey, _deadKey],
            [
                _jobHashPrefix,
                _lockKeyPrefix,
                jobId,
                "*", // Allow any owner
                reason,
                now,
                (long)retryDelay.TotalMilliseconds
            ]
        );
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(string jobId, TimeSpan? delay = null)
    {
        IDatabase db = _redis.GetDatabase();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long score = now + (long)(delay?.TotalMilliseconds ?? 0);

        // Simple release: move from active back to waiting
        await db.SetRemoveAsync(_activeKey, jobId);
        await db.KeyDeleteAsync(_lockKeyPrefix + jobId);
        await db.SortedSetAddAsync(_waitingKey, jobId, score);
    }
}
