-- requeue_retries.lua
-- Purpose: Move due jobs from retry ZSET back to waiting ZSET.
-- KEYS[1] = retryKey (valir:retry)
-- KEYS[2] = waitingKey (valir:queue:waiting)
-- ARGV[1] = now (unix timestamp ms)
-- Returns: number of requeued jobs

local retryKey = KEYS[1]
local waitingKey = KEYS[2]
local now = tonumber(ARGV[1])

-- Get all due retries
local dueJobs = redis.call('ZRANGEBYSCORE', retryKey, '-inf', now)
local count = 0

for _, jobId in ipairs(dueJobs) do
    -- Move to waiting queue
    redis.call('ZADD', waitingKey, now, jobId)
    redis.call('ZREM', retryKey, jobId)
    count = count + 1
end

return count
