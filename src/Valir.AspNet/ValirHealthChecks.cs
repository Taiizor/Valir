using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using Valir.Abstractions;

namespace Valir.AspNet;

/// <summary>
/// Health check for Redis connectivity and basic operations.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the RedisHealthCheck.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="timeout">Health check timeout.</param>
    public RedisHealthCheck(IConnectionMultiplexer redis, TimeSpan? timeout = null)
    {
        _redis = redis;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);

            IDatabase db = _redis.GetDatabase();
            string testKey = $"valir:health:{Guid.CreateVersion7():N}";

            // Test write operation
            await db.StringSetAsync(testKey, "ping", TimeSpan.FromSeconds(10), When.NotExists)
                .WaitAsync(cts.Token);

            // Test read operation
            RedisValue value = await db.StringGetAsync(testKey)
                .WaitAsync(cts.Token);

            // Cleanup
            await db.KeyDeleteAsync(testKey);

            if (value != "ping")
            {
                return HealthCheckResult.Degraded("Redis read/write consistency check failed");
            }

            // Check connection health
            if (_redis.IsConnected)
            {
                return HealthCheckResult.Healthy("Redis is connected and operational");
            }

            return HealthCheckResult.Degraded("Redis is not fully connected");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Redis health check timed out");
        }
        catch (RedisConnectionException ex)
        {
            return HealthCheckResult.Unhealthy($"Redis connection error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Redis health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Health check for job queue operations.
/// </summary>
public sealed class JobQueueHealthCheck : IHealthCheck
{
    private readonly IJobQueue _queue;
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the JobQueueHealthCheck.
    /// </summary>
    /// <param name="queue">Job queue implementation.</param>
    /// <param name="redis">Redis connection for additional checks.</param>
    /// <param name="timeout">Health check timeout.</param>
    public JobQueueHealthCheck(
        IJobQueue queue,
        IConnectionMultiplexer redis,
        TimeSpan? timeout = null)
    {
        _queue = queue;
        _redis = redis;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);

            // Test queue connectivity by attempting to claim (should return null, not throw)
            JobEnvelope? job = await _queue.ClaimAsync("health-check-worker", TimeSpan.FromSeconds(1), cts.Token);

            // Check Redis connection state
            if (!_redis.IsConnected)
            {
                return HealthCheckResult.Unhealthy("Redis connection is not available");
            }

            // Get queue statistics if possible
            IDatabase db = _redis.GetDatabase();
            var data = new Dictionary<string, object>
            {
                ["queueOperational"] = true,
                ["redisConnected"] = _redis.IsConnected,
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
            };

            return HealthCheckResult.Healthy("Job queue is operational", data);
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Job queue health check timed out");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Job queue health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Composite health check for the entire Valir system.
/// Checks Redis, job queue, and optionally message brokers.
/// </summary>
public sealed class ValirHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IJobQueue _queue;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the ValirHealthCheck.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="queue">Job queue implementation.</param>
    /// <param name="timeout">Health check timeout.</param>
    public ValirHealthCheck(
        IConnectionMultiplexer redis,
        IJobQueue queue,
        TimeSpan? timeout = null)
    {
        _redis = redis;
        _queue = queue;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        var checks = new Dictionary<string, string>();
        var data = new Dictionary<string, object>();
        bool isHealthy = true;
        bool isDegraded = false;

        try
        {
            // Check Redis connectivity
            IDatabase db = _redis.GetDatabase();
            string testKey = $"valir:health:{Guid.CreateVersion7():N}";
            await db.StringSetAsync(testKey, "ping", TimeSpan.FromSeconds(10), When.NotExists)
                .WaitAsync(cts.Token);
            await db.KeyDeleteAsync(testKey);
            checks["redis"] = "healthy";
            data["redisConnected"] = _redis.IsConnected;
        }
        catch (Exception ex)
        {
            checks["redis"] = $"unhealthy: {ex.Message}";
            isHealthy = false;
        }

        try
        {
            // Check job queue
            JobEnvelope? job = await _queue.ClaimAsync("health-check-worker", TimeSpan.FromSeconds(1), cts.Token);
            checks["queue"] = "healthy";
            data["queueOperational"] = true;
        }
        catch (Exception ex)
        {
            checks["queue"] = $"unhealthy: {ex.Message}";
            isHealthy = false;
        }

        // Add check results to data
        data["checks"] = checks;
        data["timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        if (isHealthy)
        {
            return HealthCheckResult.Healthy("All Valir components are healthy", data);
        }

        if (isDegraded)
        {
            return HealthCheckResult.Degraded("Some Valir components are degraded", exception: null, data);
        }

        return HealthCheckResult.Unhealthy("One or more Valir components are unhealthy", exception: null, data);
    }
}
