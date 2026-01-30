-- toggle_recurring.lua
-- Purpose: Enable or disable a recurring job.
-- KEYS[1] = recurring job hash key (valir:recurring:{jobId})
-- KEYS[2] = recurring schedule sorted set key (valir:recurring:schedule)
-- ARGV[1] = jobId
-- ARGV[2] = enabled (1 or 0)
-- ARGV[3] = now (timestamp ms)
-- ARGV[4] = nextExecution (timestamp ms, only used when enabling)
-- Returns: 1 on success, 0 if job not found

local jobHashKey = KEYS[1]
local scheduleKey = KEYS[2]

-- Check if job exists
local exists = redis.call('EXISTS', jobHashKey)
if exists == 0 then
    return 0
end

-- Update enabled status
redis.call('HSET', jobHashKey, 'enabled', ARGV[2], 'updatedAt', ARGV[3])

if ARGV[2] == "1" then
    -- Enabling: add back to schedule
    redis.call('ZADD', scheduleKey, ARGV[4], ARGV[1])
else
    -- Disabling: remove from schedule
    redis.call('ZREM', scheduleKey, ARGV[1])
end

return 1
