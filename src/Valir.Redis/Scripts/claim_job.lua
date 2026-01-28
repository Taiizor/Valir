-- claim_job.lua
-- Purpose: Atomically claim the next available job and assign a lock.
-- KEYS[1] = waitingKey (valir:queue:waiting)
-- KEYS[2] = activeKey (valir:queue:active)
-- ARGV[1] = jobHashPrefix (valir:job:)
-- ARGV[2] = lockKeyPrefix (valir:lock:)
-- ARGV[3] = workerId
-- ARGV[4] = now (unix timestamp ms)
-- ARGV[5] = visibilityTimeoutMs
-- Returns: {jobId, type, payload, attempts, maxAttempts} or nil

local waitingKey = KEYS[1]
local activeKey = KEYS[2]
local jobHashPrefix = ARGV[1]
local lockKeyPrefix = ARGV[2]
local workerId = ARGV[3]
local now = tonumber(ARGV[4])
local visibilityTimeoutMs = tonumber(ARGV[5])

-- Get next due job (score <= now means ready to process)
local jobs = redis.call('ZRANGEBYSCORE', waitingKey, '-inf', now, 'LIMIT', 0, 10)

for _, jobId in ipairs(jobs) do
    local lockKey = lockKeyPrefix .. jobId
    
    -- Try to acquire lock atomically
    local acquired = redis.call('SET', lockKey, workerId, 'PX', visibilityTimeoutMs, 'NX')
    
    if acquired then
        -- Move from waiting to active
        redis.call('ZREM', waitingKey, jobId)
        redis.call('SADD', activeKey, jobId)
        
        -- Update job metadata
        local jobKey = jobHashPrefix .. jobId
        redis.call('HINCRBY', jobKey, 'attempts', 1)
        redis.call('HSET', jobKey, 'visibilityDeadline', now + visibilityTimeoutMs)
        
        -- Get job data
        local jobData = redis.call('HMGET', jobKey, 'type', 'payload', 'attempts', 'maxAttempts')
        
        return {jobId, jobData[1], jobData[2], jobData[3], jobData[4]}
    end
end

return nil
