-- claim_recurring.lua
-- Purpose: Atomically claim due recurring jobs for processing.
-- KEYS[1] = schedule sorted set key (valir:recurring:schedule)
-- KEYS[2] = recurring job hash prefix (valir:recurring:)
-- KEYS[3] = lock key prefix (valir:recurring:lock:)
-- ARGV[1] = workerId
-- ARGV[2] = now (timestamp ms)
-- ARGV[3] = visibilityTimeoutMs
-- ARGV[4] = batchSize
-- Returns: Array of claimed job data {jobId, jobType, payload, queue, priority, cronExpression, cronFormat, timeZoneId, misfirePolicy, maxRetries, scheduledAt, lastExecution}

local scheduleKey = KEYS[1]
local jobHashPrefix = KEYS[2]
local lockKeyPrefix = KEYS[3]
local workerId = ARGV[1]
local now = tonumber(ARGV[2])
local visibilityTimeoutMs = tonumber(ARGV[3])
local batchSize = tonumber(ARGV[4])

-- Get jobs that are due (score <= now)
local dueJobs = redis.call('ZRANGEBYSCORE', scheduleKey, '-inf', now, 'LIMIT', 0, batchSize)
local claimedJobs = {}

for _, jobId in ipairs(dueJobs) do
    local lockKey = lockKeyPrefix .. jobId
    local jobHashKey = jobHashPrefix .. jobId

    -- Check if job is enabled
    local enabled = redis.call('HGET', jobHashKey, 'enabled')
    if enabled == "1" then
        -- Try to acquire lock (prevent multiple workers from claiming same job)
        local acquired = redis.call('SET', lockKey, workerId, 'PX', visibilityTimeoutMs, 'NX')

        if acquired then
            -- Get job data
            local jobData = redis.call('HMGET', jobHashKey,
                'jobType', 'payload', 'queue', 'priority',
                'cronExpression', 'cronFormat', 'timeZoneId',
                'misfirePolicy', 'maxRetries', 'lastExecution')

            local scheduledAt = redis.call('ZSCORE', scheduleKey, jobId)

            table.insert(claimedJobs, {
                jobId,
                jobData[1], -- jobType
                jobData[2], -- payload
                jobData[3], -- queue
                tonumber(jobData[4]) or 0, -- priority
                jobData[5], -- cronExpression
                tonumber(jobData[6]) or 0, -- cronFormat
                jobData[7], -- timeZoneId
                tonumber(jobData[8]) or 2, -- misfirePolicy (default FireOnce=2)
                tonumber(jobData[9]) or 3, -- maxRetries (default 3)
                scheduledAt, -- scheduledAt (score)
                jobData[10] -- lastExecution
            })

            -- Update last execution
            redis.call('HSET', jobHashKey, 'lastExecution', now)
        end
    end
end

return claimedJobs
