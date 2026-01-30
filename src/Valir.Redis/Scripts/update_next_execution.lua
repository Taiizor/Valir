-- update_next_execution.lua
-- Purpose: Update the next execution time for a recurring job.
-- KEYS[1] = recurring job hash key (valir:recurring:{jobId})
-- KEYS[2] = recurring schedule sorted set key (valir:recurring:schedule)
-- ARGV[1] = jobId
-- ARGV[2] = nextExecution (timestamp ms)
-- ARGV[3] = now (timestamp ms)
-- Returns: 1 on success, 0 if job not found

local jobHashKey = KEYS[1]
local scheduleKey = KEYS[2]

-- Check if job exists
local exists = redis.call('EXISTS', jobHashKey)
if exists == 0 then
    return 0
end

-- Update next execution in hash
redis.call('HSET', jobHashKey, 'nextExecution', ARGV[2], 'updatedAt', ARGV[3])

-- Update schedule sorted set
redis.call('ZADD', scheduleKey, ARGV[2], ARGV[1])

return 1
