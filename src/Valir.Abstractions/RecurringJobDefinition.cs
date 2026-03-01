namespace Valir.Abstractions;

/// <summary>
/// Defines a recurring job with all its metadata.
/// Used internally for serialization and storage.
/// </summary>
public sealed record RecurringJobDefinition
{
    /// <summary>
    /// Unique identifier for the recurring job.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Cron expression defining the schedule.
    /// </summary>
    public required string CronExpression { get; init; }

    /// <summary>
    /// The job type name used for handler routing.
    /// </summary>
    public required string JobType { get; init; }

    /// <summary>
    /// Serialized job payload as bytes.
    /// </summary>
    public required byte[] Payload { get; init; }

    /// <summary>
    /// Target queue for job instances.
    /// </summary>
    public required string Queue { get; init; }

    /// <summary>
    /// Job priority (0 = default, higher = faster).
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Cron expression format.
    /// </summary>
    public CronFormat CronFormat { get; init; }

    /// <summary>
    /// Time zone ID for the cron schedule.
    /// </summary>
    public string? TimeZoneId { get; init; }

    /// <summary>
    /// Policy for handling missed executions.
    /// </summary>
    public MisfirePolicy MisfirePolicy { get; init; }

    /// <summary>
    /// Maximum retry attempts for failed job instances.
    /// </summary>
    public int MaxRetries { get; init; }

    /// <summary>
    /// Whether the job is currently enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Timestamp when the job was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the job was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Next scheduled execution time.
    /// </summary>
    public DateTimeOffset? NextExecution { get; init; }

    /// <summary>
    /// Last execution time.
    /// </summary>
    public DateTimeOffset? LastExecution { get; init; }

    /// <summary>
    /// Creates a new RecurringJobDefinition from the provided parameters.
    /// </summary>
    public static RecurringJobDefinition Create(
        string jobId,
        string cronExpression,
        string jobType,
        byte[] payload,
        RecurringJobOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        ArgumentNullException.ThrowIfNull(payload);

        options ??= new RecurringJobOptions();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new RecurringJobDefinition
        {
            JobId = jobId,
            CronExpression = cronExpression,
            JobType = jobType,
            Payload = payload,
            Queue = options.Queue,
            Priority = options.Priority,
            CronFormat = options.CronFormat,
            TimeZoneId = options.TimeZone?.Id,
            MisfirePolicy = options.MisfirePolicy,
            MaxRetries = options.MaxRetries,
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
