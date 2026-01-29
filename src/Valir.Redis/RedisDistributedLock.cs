using StackExchange.Redis;
using Valir.Abstractions;

namespace Valir.Redis;

/// <summary>
/// Redis-backed distributed lock implementation.
/// Uses SET NX PX for atomic acquire with TTL.
/// </summary>
/// <remarks>
/// Initializes a new instance of the RedisDistributedLock.
/// </remarks>
/// <param name="redis">Redis connection.</param>
/// <param name="key">Lock key.</param>
/// <param name="owner">Lock owner identity.</param>
/// <param name="keyPrefix">Key prefix.</param>
public sealed class RedisDistributedLock(
    IConnectionMultiplexer redis,
    string key,
    string owner,
    string keyPrefix = "valir:lock:") : IDistributedLock
{
    private bool _disposed;

    /// <summary>
    /// The key being locked.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// The identity of the lock owner.
    /// </summary>
    public string Owner { get; } = owner;

    /// <inheritdoc />
    public async Task<bool> AcquireAsync(TimeSpan ttl)
    {
        IDatabase db = redis.GetDatabase();
        string lockKey = keyPrefix + Key;

        return await db.StringSetAsync(lockKey, Owner, ttl, When.NotExists);
    }

    /// <inheritdoc />
    public async Task<bool> ExtendAsync(TimeSpan ttl)
    {
        IDatabase db = redis.GetDatabase();
        string lockKey = keyPrefix + Key;

        // Atomic extend using Lua script to prevent race conditions
        // This ensures ownership verification and TTL extension happen atomically
        string script = """
            local currentOwner = redis.call('GET', KEYS[1])
            if currentOwner == false then
                return 0
            end
            if currentOwner == ARGV[1] then
                redis.call('PEXPIRE', KEYS[1], ARGV[2])
                return 1
            end
            return 0
            """;

        RedisResult result = await db.ScriptEvaluateAsync(
            script,
            [lockKey],
            [Owner, (long)ttl.TotalMilliseconds]
        );

        return (int)result == 1;
    }

    /// <inheritdoc />
    public async Task ReleaseAsync()
    {
        IDatabase db = redis.GetDatabase();
        string lockKey = keyPrefix + Key;

        // Only release if we're the owner
        string script = """
            local currentOwner = redis.call('GET', KEYS[1])
            if currentOwner == ARGV[1] then
                return redis.call('DEL', KEYS[1])
            end
            return 0
            """;

        await db.ScriptEvaluateAsync(
            script,
            [lockKey],
            [Owner]
        );
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await ReleaseAsync();
            _disposed = true;
        }
    }
}
