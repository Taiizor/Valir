using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using System.Buffers;
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
    private readonly ILogger<RedisJobQueue> _logger;
    private readonly int _maxPayloadSize;

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
    /// <param name="logger">Optional logger instance.</param>
    public RedisJobQueue(IConnectionMultiplexer redis, ValirOptions options, ILogger<RedisJobQueue>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);

        _redis = redis;
        _options = options;
        _logger = logger ?? NullLogger<RedisJobQueue>.Instance;
        _scripts = new LuaScripts();
        _maxPayloadSize = options.MaxPayloadSizeBytes > 0 ? options.MaxPayloadSizeBytes : 10 * 1024 * 1024; // Default 10 MB

        string prefix = options.KeyPrefix;
        _waitingKey = $"{prefix}queue:waiting";
        _activeKey = $"{prefix}queue:active";
        _retryKey = $"{prefix}retry";
        _deadKey = $"{prefix}dead";
        _jobHashPrefix = $"{prefix}job:";
        _lockKeyPrefix = $"{prefix}lock:";
        _payloadKeyPrefix = $"{prefix}payload:";

        _logger.LogDebug("RedisJobQueue initialized with prefix: {KeyPrefix}", prefix);
    }

    /// <summary>
    /// Initializes the queue by loading Lua scripts into Redis.
    /// Should be called once during application startup.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Loading Lua scripts into Redis...");
        await _scripts.LoadScriptsAsync(_redis);
        _logger.LogInformation("Lua scripts loaded successfully");
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

        if (payload.Length > _maxPayloadSize)
        {
            throw new ArgumentException($"Payload cannot exceed {_maxPayloadSize / (1024 * 1024)} MB.", nameof(payload));
        }

        IDatabase db = _redis.GetDatabase();
        string jobId = Guid.CreateVersion7().ToString("N");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Calculate score: lower score = higher priority + earlier time
        // Priority is negated so higher priority gets lower score
        long score = (priority * -1_000_000_000L) + now + (long)(delay?.TotalMilliseconds ?? 0);

        string jobKey = _jobHashPrefix + jobId;

        // Store job metadata using ArrayPool for high-throughput scenarios
        HashEntry[]? rentedEntries = null;
        try
        {
            rentedEntries = ArrayPool<HashEntry>.Shared.Rent(7);
            rentedEntries[0] = new("type", type);
            rentedEntries[1] = new("payload", Convert.ToBase64String(payload));
            rentedEntries[2] = new("attempts", 0);
            rentedEntries[3] = new("maxAttempts", _options.DefaultMaxAttempts);
            rentedEntries[4] = new("createdAt", now);
            rentedEntries[5] = new("priority", priority);
            rentedEntries[6] = new("idempotencyKey", idempotencyKey ?? "");

            // Create a properly sized array for the API call
            HashEntry[] hashEntries = new HashEntry[7];
            Array.Copy(rentedEntries, hashEntries, 7);

            await db.HashSetAsync(jobKey, hashEntries).ConfigureAwait(false);
        }
        finally
        {
            if (rentedEntries is not null)
            {
                ArrayPool<HashEntry>.Shared.Return(rentedEntries);
            }
        }

        await db.SortedSetAddAsync(_waitingKey, jobId, score).ConfigureAwait(false);

        _logger.LogDebug("Enqueued job {JobId} of type {JobType} with priority {Priority}", jobId, type, priority);

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

            // Use ArrayPool for batch operations
            HashEntry[]? rentedEntries = null;
            try
            {
                rentedEntries = ArrayPool<HashEntry>.Shared.Rent(7);
                rentedEntries[0] = new("type", type);
                rentedEntries[1] = new("payload", Convert.ToBase64String(payload));
                rentedEntries[2] = new("attempts", 0);
                rentedEntries[3] = new("maxAttempts", _options.DefaultMaxAttempts);
                rentedEntries[4] = new("createdAt", now);
                rentedEntries[5] = new("priority", priority);
                rentedEntries[6] = new("idempotencyKey", idempotencyKey ?? "");

                // Create a properly sized array for the API call
                HashEntry[] hashEntries = new HashEntry[7];
                Array.Copy(rentedEntries, hashEntries, 7);

                tasks.Add(batch.HashSetAsync(jobKey, hashEntries));
            }
            finally
            {
                if (rentedEntries is not null)
                {
                    ArrayPool<HashEntry>.Shared.Return(rentedEntries);
                }
            }

            tasks.Add(batch.SortedSetAddAsync(_waitingKey, jobId, score));
        }

        batch.Execute();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        _logger.LogDebug("Enqueued batch of {Count} jobs", jobIds.Count);

        return [.. jobIds];
    }

    /// <inheritdoc />
    public async Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout, CancellationToken ct = default)
    {
        IDatabase db = _redis.GetDatabase();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        RedisResult result;

        // Try using cached script hash (EVALSHA) for better performance
        if (_scripts.ClaimJobHash is not null)
        {
            try
            {
                result = await db.ScriptEvaluateAsync(
                    _scripts.ClaimJobHash,
                    [_waitingKey, _activeKey],
                    [
                        _jobHashPrefix,
                        _lockKeyPrefix,
                        workerId,
                        now,
                        (long)claimTimeout.TotalMilliseconds
                    ]
                ).ConfigureAwait(false);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT"))
            {
                // Script not in cache, fall back to EVAL
                result = await db.ScriptEvaluateAsync(
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
            }
        }
        else
        {
            result = await db.ScriptEvaluateAsync(
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
        }

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

        _logger.LogDebug("Job {JobId} claimed by worker {WorkerId}", jobId, workerId);

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

        // Try using cached script hash first
        if (_scripts.CompleteJobHash is not null)
        {
            try
            {
                await db.ScriptEvaluateAsync(
                    _scripts.CompleteJobHash,
                    [_activeKey],
                    [
                        _jobHashPrefix,
                        _lockKeyPrefix,
                        _payloadKeyPrefix,
                        jobId,
                        workerId
                    ]
                ).ConfigureAwait(false);
                return;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT"))
            {
                // Fall through to EVAL
            }
        }

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

        _logger.LogDebug("Job {JobId} completed by worker {WorkerId}", jobId, workerId);
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

        // Try using cached script hash first
        if (_scripts.FailJobHash is not null)
        {
            try
            {
                await db.ScriptEvaluateAsync(
                    _scripts.FailJobHash,
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
                return;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT"))
            {
                // Fall through to EVAL
            }
        }

        RedisResult result = await db.ScriptEvaluateAsync(
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

        string resultStr = (string)result!;
        if (resultStr == "dead")
        {
            _logger.LogError("Job {JobId} moved to dead letter queue after {Attempts} attempts. Reason: {Reason}",
                jobId, attempts, reason);
        }
        else
        {
            _logger.LogWarning("Job {JobId} failed (attempt {Attempts}/{MaxAttempts}). Retrying in {RetryDelay}s. Reason: {Reason}",
                jobId, attempts, _options.DefaultMaxAttempts, retryDelay.TotalSeconds, reason);
        }
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

        _logger.LogDebug("Job {JobId} released back to queue with delay {DelayMs}ms", jobId, delay?.TotalMilliseconds ?? 0);
    }
}
