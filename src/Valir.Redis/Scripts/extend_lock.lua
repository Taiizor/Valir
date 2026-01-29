-- extend_lock.lua
-- Purpose: Extend the lock held by a worker.
-- KEYS[1] = lockKey (valir:lock:<jobId>)
-- ARGV[1] = workerId
-- ARGV[2] = extensionMs (milliseconds)
-- Returns: 1 if lock extended, 0 otherwise

local lockKey = KEYS[1]
local workerId = ARGV[1]
local extensionMs = tonumber(ARGV[2])
        
local currentOwner = redis.call('GET', lockKey)
        
if currentOwner == false or currentOwner ~= workerId then
    return 0
end
        
local currentTtl = redis.call('PTTL', lockKey)
if currentTtl < 0 then
    return 0
end
        
local newTtl = currentTtl + extensionMs
redis.call('PEXPIRE', lockKey, newTtl)
        
return 1
