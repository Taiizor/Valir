-- complete_job.lua
-- Purpose: Atomically mark a job as complete and clean up.
-- KEYS[1] = activeKey (valir:queue:active)
-- ARGV[1] = jobHashPrefix (valir:job:)
-- ARGV[2] = lockKeyPrefix (valir:lock:)
-- ARGV[3] = payloadKeyPrefix (valir:payload:)
-- ARGV[4] = jobId
-- ARGV[5] = workerId
-- Returns: 1 = success, 0 = owner mismatch

local activeKey = KEYS[1]
local jobHashPrefix = ARGV[1]
local lockKeyPrefix = ARGV[2]
local payloadKeyPrefix = ARGV[3]
local jobId = ARGV[4]
local workerId = ARGV[5]

local lockKey = lockKeyPrefix .. jobId

-- Verify ownership
local currentOwner = redis.call('GET', lockKey)
if currentOwner ~= workerId then
    return 0
end

-- Remove from active set
redis.call('SREM', activeKey, jobId)

-- Delete lock
redis.call('DEL', lockKey)

-- Delete job hash
local jobKey = jobHashPrefix .. jobId
redis.call('DEL', jobKey)

-- Delete payload if stored separately
local payloadKey = payloadKeyPrefix .. jobId
redis.call('DEL', payloadKey)

return 1
