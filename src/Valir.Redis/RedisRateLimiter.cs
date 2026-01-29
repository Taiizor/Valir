using StackExchange.Redis;
using Valir.Abstractions;

namespace Valir.Redis;

/// <summary>
/// Redis-backed rate limiter using sliding window algorithm.
/// </summary>
/// <remarks>
/// Initializes a new instance of the RedisRateLimiter.
/// </remarks>
/// <param name="redis">Redis connection.</param>
/// <param name="keyPrefix">Prefix for rate limit keys.</param>
public sealed class RedisRateLimiter(IConnectionMultiplexer redis, string keyPrefix = "valir:rate:") : IRateLimiter
{
    /// <inheritdoc />
    public async Task<bool> AllowAsync(string key, int max, TimeSpan window)
    {
        IDatabase db = redis.GetDatabase();
        string rateKey = keyPrefix + key;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long windowStart = now - (long)window.TotalMilliseconds;

        // Sliding window using sorted set
        string script = """
            local key = KEYS[1]
            local now = tonumber(ARGV[1])
            local windowStart = tonumber(ARGV[2])
            local max = tonumber(ARGV[3])
            local windowMs = tonumber(ARGV[4])
            
            -- Remove expired entries
            redis.call('ZREMRANGEBYSCORE', key, '-inf', windowStart)
            
            -- Count current entries
            local count = redis.call('ZCARD', key)
            
            if count < max then
                -- Add new entry
                redis.call('ZADD', key, now, now .. ':' .. math.random())
                redis.call('PEXPIRE', key, windowMs)
                return 1
            end
            
            return 0
            """;

        RedisResult result = await db.ScriptEvaluateAsync(
            script,
            [rateKey],
            [now, windowStart, max, (long)window.TotalMilliseconds]
        );

        return (int)result == 1;
    }
}
