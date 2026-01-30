-- delete_recurring.lua
-- Purpose: Permanently delete a recurring job and all its data.
-- KEYS[1] = recurring job hash key (valir:recurring:{jobId})
-- KEYS[2] = recurring jobs set key (valir:recurring:jobs)
-- KEYS[3] = recurring schedule sorted set key (valir:recurring:schedule)
-- KEYS[4] = lock key (valir:recurring:lock:{jobId})
-- ARGV[1] = jobId
-- Returns: 1 on success

local jobHashKey = KEYS[1]
local jobsSetKey = KEYS[2]
local scheduleKey = KEYS[3]
local lockKey = KEYS[4]

-- Remove from all structures
redis.call('DEL', jobHashKey)
redis.call('SREM', jobsSetKey, ARGV[1])
redis.call('ZREM', scheduleKey, ARGV[1])
redis.call('DEL', lockKey)

return 1
