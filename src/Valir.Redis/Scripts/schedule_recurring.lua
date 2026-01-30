-- schedule_recurring.lua
-- Purpose: Atomically store or update a recurring job definition.
-- KEYS[1] = recurring job hash key (valir:recurring:{jobId})
-- KEYS[2] = recurring jobs set key (valir:recurring:jobs)
-- KEYS[3] = recurring schedule sorted set key (valir:recurring:schedule)
-- ARGV[1] = jobId
-- ARGV[2] = cronExpression
-- ARGV[3] = timeZoneId (or empty string)
-- ARGV[4] = jobType
-- ARGV[5] = payload (base64)
-- ARGV[6] = queue
-- ARGV[7] = priority
-- ARGV[8] = cronFormat (0=Standard, 1=IncludeSeconds)
-- ARGV[9] = now (timestamp ms)
-- ARGV[10] = nextExecution (timestamp ms)
-- ARGV[11] = misfirePolicy (0=Skip, 1=FireAll, 2=FireOnce, 3=FireNow)
-- ARGV[12] = maxRetries
-- ARGV[13] = enabled (1 or 0)
-- Returns: 1 on success

local jobHashKey = KEYS[1]
local jobsSetKey = KEYS[2]
local scheduleKey = KEYS[3]

-- Store job definition
redis.call('HMSET', jobHashKey,
    'cronExpression', ARGV[2],
    'timeZoneId', ARGV[3],
    'jobType', ARGV[4],
    'payload', ARGV[5],
    'queue', ARGV[6],
    'priority', ARGV[7],
    'cronFormat', ARGV[8],
    'createdAt', ARGV[9],
    'updatedAt', ARGV[9],
    'nextExecution', ARGV[10],
    'misfirePolicy', ARGV[11],
    'maxRetries', ARGV[12],
    'enabled', ARGV[13]
)

-- Add to jobs set
redis.call('SADD', jobsSetKey, ARGV[1])

-- Add/update schedule sorted set
redis.call('ZADD', scheduleKey, ARGV[10], ARGV[1])

return 1
