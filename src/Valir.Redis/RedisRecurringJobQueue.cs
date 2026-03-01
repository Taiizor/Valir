using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Valir.Abstractions;
using Valir.Core;

namespace Valir.Redis;

/// <summary>
/// Redis-backed implementation of IRecurringJobQueue using Lua scripts for atomicity.
/// </summary>
public sealed class RedisRecurringJobQueue : IRecurringJobQueue
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ValirOptions _options;
    private readonly ILogger<RedisRecurringJobQueue> _logger;

    // Key names
    private readonly string _recurringHashPrefix;
    private readonly string _recurringJobsSetKey;
    private readonly string _recurringScheduleKey;
    private readonly string _recurringLockPrefix;

    // Lua script hashes (loaded on initialization)
    private byte[]? _scheduleScriptHash;
    private byte[]? _claimScriptHash;
    private byte[]? _updateNextExecutionScriptHash;
    private byte[]? _deleteScriptHash;
    private byte[]? _toggleScriptHash;

    /// <summary>
    /// Initializes a new instance of the RedisRecurringJobQueue.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="logger">Optional logger instance.</param>
    public RedisRecurringJobQueue(
        IConnectionMultiplexer redis,
        ValirOptions options,
        ILogger<RedisRecurringJobQueue>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);

        _redis = redis;
        _options = options;
        _logger = logger ?? NullLogger<RedisRecurringJobQueue>.Instance;

        string prefix = options.KeyPrefix;
        _recurringHashPrefix = $"{prefix}recurring:";
        _recurringJobsSetKey = $"{prefix}recurring:jobs";
        _recurringScheduleKey = $"{prefix}recurring:schedule";
        _recurringLockPrefix = $"{prefix}recurring:lock:";

        _logger.LogDebug("RedisRecurringJobQueue initialized with prefix: {KeyPrefix}", prefix);
    }

    /// <summary>
    /// Initializes the queue by loading Lua scripts into Redis.
    /// Should be called once during application startup.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Loading recurring job Lua scripts into Redis...");

        IServer server = _redis.GetServer(_redis.GetEndPoints().First());

        _scheduleScriptHash = await server.ScriptLoadAsync(ScheduleRecurringScript);
        _claimScriptHash = await server.ScriptLoadAsync(ClaimRecurringScript);
        _updateNextExecutionScriptHash = await server.ScriptLoadAsync(UpdateNextExecutionScript);
        _deleteScriptHash = await server.ScriptLoadAsync(DeleteRecurringScript);
        _toggleScriptHash = await server.ScriptLoadAsync(ToggleRecurringScript);

        _logger.LogInformation("Recurring job Lua scripts loaded successfully");
    }

    /// <inheritdoc />
    public async Task ScheduleAsync(
        string jobId,
        string cronExpression,
        string jobType,
        byte[] payload,
        RecurringJobOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        ArgumentNullException.ThrowIfNull(payload);

        options ??= new RecurringJobOptions();

        // Calculate next execution using Cronos
        Cronos.CronFormat cronFormat = options.CronFormat == CronFormat.IncludeSeconds
            ? Cronos.CronFormat.IncludeSeconds
            : Cronos.CronFormat.Standard;

        Cronos.CronExpression expression = Cronos.CronExpression.Parse(cronExpression, cronFormat);
        TimeZoneInfo timeZone = options.TimeZone ?? TimeZoneInfo.Utc;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DateTime? nextOccurrence = expression.GetNextOccurrence(now.UtcDateTime, timeZone);
        if (!nextOccurrence.HasValue)
        {
            throw new InvalidOperationException($"Could not calculate next occurrence for cron expression: {cronExpression}");
        }

        DateTime nextOccurrenceValue = nextOccurrence.Value;
        DateTime nextUtc = nextOccurrenceValue.Kind switch
        {
            DateTimeKind.Utc => nextOccurrenceValue,
            DateTimeKind.Local => nextOccurrenceValue.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(nextOccurrenceValue, timeZone)
        };

        DateTimeOffset nextExecution = new(nextUtc, TimeSpan.Zero);
        long nowMs = now.ToUnixTimeMilliseconds();
        long nextExecutionMs = nextExecution.ToUnixTimeMilliseconds();

        // Encode payload to base64 for Redis storage
        string payloadBase64 = Convert.ToBase64String(payload);

        IDatabase db = _redis.GetDatabase();

        RedisKey[] keys =
        [
            _recurringHashPrefix + jobId,
            _recurringJobsSetKey,
            _recurringScheduleKey
        ];

        RedisValue[] values =
        [
            jobId,
            cronExpression,
            options.TimeZone?.Id ?? "",
            jobType,
            payloadBase64,
            options.Queue,
            options.Priority,
            (int)options.CronFormat,
            nowMs,
            nextExecutionMs,
            (int)options.MisfirePolicy,
            options.MaxRetries,
            1 // enabled
        ];

        await db.ScriptEvaluateAsync(
            _scheduleScriptHash!,
            keys,
            values,
            CommandFlags.DemandMaster);

        _logger.LogInformation(
            "Scheduled recurring job {JobId} with cron '{CronExpression}', next execution: {NextExecution}",
            jobId, cronExpression, nextExecution);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        IDatabase db = _redis.GetDatabase();

        RedisKey[] keys =
        [
            _recurringHashPrefix + jobId,
            _recurringJobsSetKey,
            _recurringScheduleKey,
            _recurringLockPrefix + jobId
        ];

        RedisValue[] values = [jobId];

        await db.ScriptEvaluateAsync(
            _deleteScriptHash!,
            keys,
            values,
            CommandFlags.DemandMaster);

        _logger.LogInformation("Removed recurring job {JobId}", jobId);
    }

    /// <inheritdoc />
    public async Task DisableAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        await ToggleAsync(jobId, enabled: false, ct);
    }

    /// <inheritdoc />
    public async Task EnableAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        await ToggleAsync(jobId, enabled: true, ct);
    }

    private async Task ToggleAsync(string jobId, bool enabled, CancellationToken ct)
    {
        IDatabase db = _redis.GetDatabase();

        // Get current job data to calculate next execution if enabling
        long? nextExecutionMs = null;
        if (enabled)
        {
            RedisValue[] jobData = await db.HashGetAsync(_recurringHashPrefix + jobId, ["cronExpression", "cronFormat", "timeZoneId"]);
            if (jobData[0].IsNull)
            {
                throw new InvalidOperationException($"Recurring job {jobId} not found");
            }

            string cronExpression = jobData[0].ToString();
            CronFormat cronFormat = (CronFormat)(int)jobData[1];
            string timeZoneId = jobData[2].ToString();

            TimeZoneInfo timeZone = string.IsNullOrEmpty(timeZoneId)
                ? TimeZoneInfo.Utc
                : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

            Cronos.CronFormat format = cronFormat == CronFormat.IncludeSeconds
                ? Cronos.CronFormat.IncludeSeconds
                : Cronos.CronFormat.Standard;

            Cronos.CronExpression expression = Cronos.CronExpression.Parse(cronExpression, format);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            DateTime? nextOccurrence = expression.GetNextOccurrence(now.UtcDateTime, timeZone);
            if (nextOccurrence.HasValue)
            {
                DateTimeOffset nextExecution = new(nextOccurrence.Value, timeZone.GetUtcOffset(nextOccurrence.Value));
                nextExecutionMs = nextExecution.ToUnixTimeMilliseconds();
            }
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        RedisKey[] keys =
        [
            _recurringHashPrefix + jobId,
            _recurringScheduleKey
        ];

        RedisValue[] values =
        [
            jobId,
            enabled ? 1 : 0,
            nowMs,
            nextExecutionMs ?? 0
        ];

        RedisResult result = await db.ScriptEvaluateAsync(
            _toggleScriptHash!,
            keys,
            values,
            CommandFlags.DemandMaster);

        if ((int)result == 0)
        {
            throw new InvalidOperationException($"Recurring job {jobId} not found");
        }

        _logger.LogInformation("{Action} recurring job {JobId}", enabled ? "Enabled" : "Disabled", jobId);
    }

    /// <inheritdoc />
    public async Task<RecurringJobClaimResult[]> ClaimDueJobsAsync(
        string workerId,
        int batchSize = 10,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        IDatabase db = _redis.GetDatabase();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        long nowMs = now.ToUnixTimeMilliseconds();
        long visibilityTimeoutMs = (long)_options.DefaultVisibilityTimeout.TotalMilliseconds;

        RedisKey[] keys =
        [
            _recurringScheduleKey,
            _recurringHashPrefix,
            _recurringLockPrefix
        ];

        RedisValue[] values =
        [
            workerId,
            nowMs,
            visibilityTimeoutMs,
            batchSize
        ];

        RedisResult result = await db.ScriptEvaluateAsync(
            _claimScriptHash!,
            keys,
            values,
            CommandFlags.DemandMaster);

        List<RecurringJobClaimResult> claimedJobs = [];

        if (result.Resp3Type == ResultType.Array)
        {
            RedisResult[] results = (RedisResult[])result!;
            foreach (RedisResult jobResult in results)
            {
                if (jobResult.Resp3Type == ResultType.Array)
                {
                    RedisResult[]? jobData = (RedisResult[])jobResult;
                    if (jobData?.Length >= 12)
                    {
                        string jobId = jobData[0].ToString()!;
                        string jobType = jobData[1].ToString()!;
                        string payloadBase64 = jobData[2].ToString()!;
                        string queue = jobData[3].ToString()!;
                        int priority = (int)jobData[4];
                        string cronExpression = jobData[5].ToString()!;
                        CronFormat cronFormat = (CronFormat)(int)jobData[6];
                        string timeZoneId = jobData[7].ToString();
                        MisfirePolicy misfirePolicy = (MisfirePolicy)(int)jobData[8];
                        int maxRetries = (int)jobData[9];
                        long scheduledAtMs = (long)jobData[10];
                        string lastExecutionStr = jobData[11].ToString();

                        byte[] payload = Convert.FromBase64String(payloadBase64);
                        DateTimeOffset scheduledAt = DateTimeOffset.FromUnixTimeMilliseconds(scheduledAtMs);
                        DateTimeOffset? lastExecution = null;
                        if (!string.IsNullOrEmpty(lastExecutionStr) && long.TryParse(lastExecutionStr, out long lastMs))
                        {
                            lastExecution = DateTimeOffset.FromUnixTimeMilliseconds(lastMs);
                        }

                        TimeZoneInfo? timeZone = null;
                        if (!string.IsNullOrEmpty(timeZoneId))
                        {
                            try
                            {
                                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                            }
                            catch (TimeZoneNotFoundException)
                            {
                                _logger.LogWarning("Time zone {TimeZoneId} not found for job {JobId}, using UTC", timeZoneId, jobId);
                                timeZone = TimeZoneInfo.Utc;
                            }
                        }

                        claimedJobs.Add(new RecurringJobClaimResult(
                            jobId,
                            jobType,
                            payload,
                            queue,
                            priority,
                            cronExpression,
                            cronFormat,
                            timeZone,
                            misfirePolicy,
                            maxRetries,
                            scheduledAt,
                            lastExecution
                        ));
                    }
                }
            }
        }

        return [.. claimedJobs];
    }

    /// <inheritdoc />
    public async Task UpdateNextExecutionAsync(
        string jobId,
        DateTimeOffset nextExecution,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        IDatabase db = _redis.GetDatabase();

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long nextExecutionMs = nextExecution.ToUnixTimeMilliseconds();

        RedisKey[] keys =
        [
            _recurringHashPrefix + jobId,
            _recurringScheduleKey
        ];

        RedisValue[] values =
        [
            jobId,
            nextExecutionMs,
            nowMs
        ];

        RedisResult result = await db.ScriptEvaluateAsync(
            _updateNextExecutionScriptHash!,
            keys,
            values,
            CommandFlags.DemandMaster);

        if ((int)result == 0)
        {
            _logger.LogWarning("Could not update next execution for recurring job {JobId} - job not found", jobId);
        }
    }

    /// <inheritdoc />
    public async Task<RecurringJobInfo[]> GetAllAsync(CancellationToken ct = default)
    {
        IDatabase db = _redis.GetDatabase();

        // Get all job IDs from the set
        RedisValue[] jobIds = await db.SetMembersAsync(_recurringJobsSetKey);

        List<RecurringJobInfo> jobs = [];

        foreach (RedisValue jobId in jobIds)
        {
            RecurringJobInfo? job = await GetAsync(jobId.ToString(), ct);
            if (job is not null)
            {
                jobs.Add(job);
            }
        }

        return [.. jobs];
    }

    /// <inheritdoc />
    public async Task<RecurringJobInfo?> GetAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        IDatabase db = _redis.GetDatabase();

        string hashKey = _recurringHashPrefix + jobId;
        RedisValue[] fields =
        [
            "cronExpression", "jobType", "queue", "priority",
            "cronFormat", "timeZoneId", "misfirePolicy", "maxRetries",
            "enabled", "nextExecution", "lastExecution", "createdAt", "updatedAt"
        ];

        RedisValue[] values = await db.HashGetAsync(hashKey, fields);

        if (values[0].IsNull)
        {
            return null;
        }

        string cronExpression = values[0].ToString()!;
        string jobType = values[1].ToString()!;
        string queue = values[2].ToString()!;
        int priority = (int)values[3];
        CronFormat cronFormat = (CronFormat)(int)values[4];
        string timeZoneId = values[5].ToString();
        MisfirePolicy misfirePolicy = (MisfirePolicy)(int)values[6];
        int maxRetries = (int)values[7];
        bool enabled = (int)values[8] == 1;

        DateTimeOffset? nextExecution = null;
        if (!values[9].IsNull && long.TryParse(values[9].ToString(), out long nextMs))
        {
            nextExecution = DateTimeOffset.FromUnixTimeMilliseconds(nextMs);
        }

        DateTimeOffset? lastExecution = null;
        if (!values[10].IsNull && long.TryParse(values[10].ToString(), out long lastMs))
        {
            lastExecution = DateTimeOffset.FromUnixTimeMilliseconds(lastMs);
        }

        DateTimeOffset createdAt = DateTimeOffset.FromUnixTimeMilliseconds((long)values[11]);
        DateTimeOffset updatedAt = DateTimeOffset.FromUnixTimeMilliseconds((long)values[12]);

        TimeZoneInfo? timeZone = null;
        if (!string.IsNullOrEmpty(timeZoneId))
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                timeZone = TimeZoneInfo.Utc;
            }
        }

        return new RecurringJobInfo(
            jobId,
            cronExpression,
            jobType,
            queue,
            priority,
            timeZone,
            misfirePolicy,
            maxRetries,
            enabled,
            nextExecution,
            lastExecution,
            createdAt,
            updatedAt
        );
    }

    // Lua Scripts
    private const string ScheduleRecurringScript = """
        local jobHashKey = KEYS[1]
        local jobsSetKey = KEYS[2]
        local scheduleKey = KEYS[3]

        local existingCreatedAt = redis.call('HGET', jobHashKey, 'createdAt')
        local createdAt = existingCreatedAt or ARGV[9]

        redis.call('HMSET', jobHashKey,
            'cronExpression', ARGV[2],
            'timeZoneId', ARGV[3],
            'jobType', ARGV[4],
            'payload', ARGV[5],
            'queue', ARGV[6],
            'priority', ARGV[7],
            'cronFormat', ARGV[8],
            'createdAt', createdAt,
            'updatedAt', ARGV[9],
            'nextExecution', ARGV[10],
            'misfirePolicy', ARGV[11],
            'maxRetries', ARGV[12],
            'enabled', ARGV[13]
        )

        redis.call('SADD', jobsSetKey, ARGV[1])
        redis.call('ZADD', scheduleKey, ARGV[10], ARGV[1])

        return 1
        """;

    private const string ClaimRecurringScript = """
        local scheduleKey = KEYS[1]
        local jobHashPrefix = KEYS[2]
        local lockKeyPrefix = KEYS[3]
        local workerId = ARGV[1]
        local now = tonumber(ARGV[2])
        local visibilityTimeoutMs = tonumber(ARGV[3])
        local batchSize = tonumber(ARGV[4])

        local dueJobs = redis.call('ZRANGEBYSCORE', scheduleKey, '-inf', now, 'LIMIT', 0, batchSize)
        local claimedJobs = {}

        for _, jobId in ipairs(dueJobs) do
            local lockKey = lockKeyPrefix .. jobId
            local jobHashKey = jobHashPrefix .. jobId

            local enabled = redis.call('HGET', jobHashKey, 'enabled')
            if enabled == "1" then
                local acquired = redis.call('SET', lockKey, workerId, 'PX', visibilityTimeoutMs, 'NX')

                if acquired then
                    local jobData = redis.call('HMGET', jobHashKey,
                        'jobType', 'payload', 'queue', 'priority',
                        'cronExpression', 'cronFormat', 'timeZoneId',
                        'misfirePolicy', 'maxRetries', 'lastExecution')

                    local scheduledAt = redis.call('ZSCORE', scheduleKey, jobId)

                    table.insert(claimedJobs, {
                        jobId,
                        jobData[1],
                        jobData[2],
                        jobData[3],
                        tonumber(jobData[4]) or 0,
                        jobData[5],
                        tonumber(jobData[6]) or 0,
                        jobData[7],
                        tonumber(jobData[8]) or 2,
                        tonumber(jobData[9]) or 3,
                        scheduledAt,
                        jobData[10]
                    })

                    redis.call('HSET', jobHashKey, 'lastExecution', now)
                end
            end
        end

        return claimedJobs
        """;

    private const string UpdateNextExecutionScript = """
        local jobHashKey = KEYS[1]
        local scheduleKey = KEYS[2]

        local exists = redis.call('EXISTS', jobHashKey)
        if exists == 0 then
            return 0
        end

        redis.call('HSET', jobHashKey, 'nextExecution', ARGV[2], 'updatedAt', ARGV[3])
        redis.call('ZADD', scheduleKey, ARGV[2], ARGV[1])

        return 1
        """;

    private const string DeleteRecurringScript = """
        local jobHashKey = KEYS[1]
        local jobsSetKey = KEYS[2]
        local scheduleKey = KEYS[3]
        local lockKey = KEYS[4]

        redis.call('DEL', jobHashKey)
        redis.call('SREM', jobsSetKey, ARGV[1])
        redis.call('ZREM', scheduleKey, ARGV[1])
        redis.call('DEL', lockKey)

        return 1
        """;

    private const string ToggleRecurringScript = """
        local jobHashKey = KEYS[1]
        local scheduleKey = KEYS[2]

        local exists = redis.call('EXISTS', jobHashKey)
        if exists == 0 then
            return 0
        end

        redis.call('HSET', jobHashKey, 'enabled', ARGV[2], 'updatedAt', ARGV[3])

        if ARGV[2] == "1" then
            redis.call('ZADD', scheduleKey, ARGV[4], ARGV[1])
        else
            redis.call('ZREM', scheduleKey, ARGV[1])
        end

        return 1
        """;
}
