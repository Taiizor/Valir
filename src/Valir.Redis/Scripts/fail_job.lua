-- fail_job.lua
-- Purpose: Mark a job as failed, schedule retry or dead-letter.
-- KEYS[1] = activeKey (valir:queue:active)
-- KEYS[2] = retryKey (valir:retry)
-- KEYS[3] = deadKey (valir:dead)
-- ARGV[1] = jobHashPrefix (valir:job:)
-- ARGV[2] = lockKeyPrefix (valir:lock:)
-- ARGV[3] = jobId
-- ARGV[4] = workerId
-- ARGV[5] = reason
-- ARGV[6] = now (unix timestamp ms)
-- ARGV[7] = retryDelayMs
-- Returns: "retry", "dead", or "error"

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

-- Verify ownership
local currentOwner = redis.call('GET', lockKey)
if currentOwner ~= workerId then
    return "error"
end

-- Get current attempts
local attempts = tonumber(redis.call('HGET', jobKey, 'attempts') or 0)
local maxAttempts = tonumber(redis.call('HGET', jobKey, 'maxAttempts') or 3)

-- Remove from active
redis.call('SREM', activeKey, jobId)
-- Release lock
redis.call('DEL', lockKey)

-- Store failure reason
redis.call('HSET', jobKey, 'lastError', reason, 'failedAt', now)

if attempts >= maxAttempts then
    -- Dead-letter
    redis.call('RPUSH', deadKey, jobId)
    return "dead"
else
    -- Schedule retry
    local nextRetryAt = now + retryDelayMs
    redis.call('ZADD', retryKey, nextRetryAt, jobId)
    return "retry"
end
