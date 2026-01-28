namespace Valir.Redis;

/// <summary>
/// Manages Lua script strings for atomic Redis operations.
/// These scripts ensure consistency in multi-command operations.
/// </summary>
internal sealed class LuaScripts
{
    /// <summary>
    /// Lua script for atomically claiming a job from the waiting queue.
    /// </summary>
    public string ClaimJob { get; }

    /// <summary>
    /// Lua script for atomically completing a job and cleaning up.
    /// </summary>
    public string CompleteJob { get; }

    /// <summary>
    /// Lua script for failing a job and scheduling retry or dead-letter.
    /// </summary>
    public string FailJob { get; }

    /// <summary>
    /// Lua script for moving due retry jobs back to the waiting queue.
    /// </summary>
    public string RequeueRetries { get; }

    /// <summary>
    /// Initialize all Lua scripts.
    /// </summary>
    public LuaScripts()
    {
        ClaimJob = ClaimJobScript;
        CompleteJob = CompleteJobScript;
        FailJob = FailJobScript;
        RequeueRetries = RequeueRetriesScript;
    }

    private const string ClaimJobScript = """
        local waitingKey = KEYS[1]
        local activeKey = KEYS[2]
        local jobHashPrefix = ARGV[1]
        local lockKeyPrefix = ARGV[2]
        local workerId = ARGV[3]
        local now = tonumber(ARGV[4])
        local visibilityTimeoutMs = tonumber(ARGV[5])
        
        local jobs = redis.call('ZRANGEBYSCORE', waitingKey, '-inf', now, 'LIMIT', 0, 10)
        
        for _, jobId in ipairs(jobs) do
            local lockKey = lockKeyPrefix .. jobId
            local acquired = redis.call('SET', lockKey, workerId, 'PX', visibilityTimeoutMs, 'NX')
            
            if acquired then
                redis.call('ZREM', waitingKey, jobId)
                redis.call('SADD', activeKey, jobId)
                
                local jobKey = jobHashPrefix .. jobId
                redis.call('HINCRBY', jobKey, 'attempts', 1)
                redis.call('HSET', jobKey, 'visibilityDeadline', now + visibilityTimeoutMs)
                
                local jobData = redis.call('HMGET', jobKey, 'type', 'payload', 'attempts', 'maxAttempts')
                return {jobId, jobData[1], jobData[2], jobData[3], jobData[4]}
            end
        end
        
        return nil
        """;

    private const string CompleteJobScript = """
        local activeKey = KEYS[1]
        local jobHashPrefix = ARGV[1]
        local lockKeyPrefix = ARGV[2]
        local payloadKeyPrefix = ARGV[3]
        local jobId = ARGV[4]
        local workerId = ARGV[5]
        
        local lockKey = lockKeyPrefix .. jobId
        local currentOwner = redis.call('GET', lockKey)
        
        if workerId ~= '*' and currentOwner ~= workerId then
            return 0
        end
        
        redis.call('SREM', activeKey, jobId)
        redis.call('DEL', lockKey)
        redis.call('DEL', jobHashPrefix .. jobId)
        redis.call('DEL', payloadKeyPrefix .. jobId)
        
        return 1
        """;

    private const string FailJobScript = """
        local activeKey = KEYS[1]
        local retryKey = KEYS[2]
        local deadKey = KEYS[3]
        local jobHashPrefix = ARGV[1]
        local lockKeyPrefix = ARGV[2]
        local jobId = ARGV[3]
        local workerId = ARGV[4]
        local reason = ARGV[5]
        local now = tonumber(ARGV[6])
        local retryDelayMs = tonumber(ARGV[7])
        
        local lockKey = lockKeyPrefix .. jobId
        local jobKey = jobHashPrefix .. jobId
        local currentOwner = redis.call('GET', lockKey)
        
        if workerId ~= '*' and currentOwner ~= workerId then
            return 'error'
        end
        
        local attempts = tonumber(redis.call('HGET', jobKey, 'attempts') or 0)
        local maxAttempts = tonumber(redis.call('HGET', jobKey, 'maxAttempts') or 3)
        
        redis.call('SREM', activeKey, jobId)
        redis.call('DEL', lockKey)
        redis.call('HSET', jobKey, 'lastError', reason, 'failedAt', now)
        
        if attempts >= maxAttempts then
            redis.call('RPUSH', deadKey, jobId)
            return 'dead'
        else
            local nextRetryAt = now + retryDelayMs
            redis.call('ZADD', retryKey, nextRetryAt, jobId)
            return 'retry'
        end
        """;

    private const string RequeueRetriesScript = """
        local retryKey = KEYS[1]
        local waitingKey = KEYS[2]
        local now = tonumber(ARGV[1])
        
        local dueJobs = redis.call('ZRANGEBYSCORE', retryKey, '-inf', now)
        local count = 0
        
        for _, jobId in ipairs(dueJobs) do
            redis.call('ZADD', waitingKey, now, jobId)
            redis.call('ZREM', retryKey, jobId)
            count = count + 1
        end
        
        return count
        """;
}
