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
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        // Validate job type parameter
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Job type cannot be null or empty.", nameof(type));
        }

        if (type.Length > 256)
        {
            throw new ArgumentException("Job type cannot exceed 256 characters.", nameof(type));
        }

        // Validate type format (alphanumeric, dots, hyphens, underscores only)
        foreach (char c in type)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_')
            {
                throw new ArgumentException("Job type can only contain alphanumeric characters, dots, hyphens, and underscores.", nameof(type));
            }
        }

        if (payload is null || payload.Length == 0)
        {
            throw new ArgumentException("Payload cannot be null or empty.", nameof(payload));
        }

        if (payload.Length > 10 * 1024 * 1024) // 10 MB limit
        {
            throw new ArgumentException("Payload cannot exceed 10 MB.", nameof(payload));
        }

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

        await db.HashSetAsync(jobKey, hashEntries).ConfigureAwait(false);
        await db.SortedSetAddAsync(_waitingKey, jobId, score).ConfigureAwait(false);

        return jobId;
    }

    /// <inheritdoc />
    public async Task<string[]> EnqueueBatchAsync(
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs,
        int priority = 0,
        CancellationToken ct = default)
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
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return [.. jobIds];
    }

    /// <inheritdoc />
    public async Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout, CancellationToken ct = default)
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
        ).ConfigureAwait(false);

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
    public async Task CompleteAsync(string jobId, CancellationToken ct = default)
    {
        IDatabase db = _redis.GetDatabase();

        // Get the worker ID from the lock key to verify ownership
        string lockKey = _lockKeyPrefix + jobId;
        RedisValue lockOwner = await db.StringGetAsync(lockKey).ConfigureAwait(false);
        string workerId = lockOwner.IsNull ? "*" : (string)lockOwner!;

        await db.ScriptEvaluateAsync(
            _scripts.CompleteJob,
            [_activeKey],
            [
                _jobHashPrefix,
                _lockKeyPrefix,
                _payloadKeyPrefix,
                jobId,
                workerId
            ]
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task FailAsync(string jobId, string reason, CancellationToken ct = default)
    {
        IDatabase db = _redis.GetDatabase();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Get the worker ID from the lock key to verify ownership
        string lockKey = _lockKeyPrefix + jobId;
        RedisValue lockOwner = await db.StringGetAsync(lockKey).ConfigureAwait(false);
        string workerId = lockOwner.IsNull ? "*" : (string)lockOwner!;

        // Get current attempts for backoff calculation
        string jobKey = _jobHashPrefix + jobId;
        int attempts = (int)await db.HashGetAsync(jobKey, "attempts").ConfigureAwait(false);
        TimeSpan retryDelay = RetryPolicy.CalculateDelay(attempts, _options.RetryBaseDelay);

        await db.ScriptEvaluateAsync(
            _scripts.FailJob,
            [_activeKey, _retryKey, _deadKey],
            [
                _jobHashPrefix,
                _lockKeyPrefix,
                jobId,
                workerId,
                reason,
                now,
                (long)retryDelay.TotalMilliseconds
            ]
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(string jobId, TimeSpan? delay = null, CancellationToken ct = default)
    {
        IDatabase db = _redis.GetDatabase();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long score = now + (long)(delay?.TotalMilliseconds ?? 0);

        // Simple release: move from active back to waiting
        await db.SetRemoveAsync(_activeKey, jobId).ConfigureAwait(false);
        await db.KeyDeleteAsync(_lockKeyPrefix + jobId).ConfigureAwait(false);
        await db.SortedSetAddAsync(_waitingKey, jobId, score).ConfigureAwait(false);
    }
}
