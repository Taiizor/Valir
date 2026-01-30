namespace Valir.Abstractions;

/// <summary>
/// Interface for managing recurring jobs with cron-based scheduling.
/// </summary>
public interface IRecurringJobQueue
{
    /// <summary>
    /// Schedules a new recurring job or updates an existing one.
    /// </summary>
    /// <param name="jobId">Unique identifier for the recurring job.</param>
    /// <param name="cronExpression">Cron expression defining the schedule.</param>
    /// <param name="jobType">The job type name used for handler routing.</param>
    /// <param name="payload">Serialized job payload as bytes.</param>
    /// <param name="options">Optional configuration for the recurring job.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ScheduleAsync(
        string jobId,
        string cronExpression,
        string jobType,
        byte[] payload,
        RecurringJobOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a recurring job permanently.
    /// </summary>
    /// <param name="jobId">The unique identifier of the recurring job to remove.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Temporarily disables a recurring job without removing it.
    /// </summary>
    /// <param name="jobId">The unique identifier of the recurring job to disable.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisableAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Re-enables a previously disabled recurring job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the recurring job to enable.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnableAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Claims due recurring jobs for processing. Used by SchedulerWorker.
    /// </summary>
    /// <param name="workerId">Unique identifier of the claiming worker.</param>
    /// <param name="batchSize">Maximum number of jobs to claim.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Array of claimed recurring job results.</returns>
    Task<RecurringJobClaimResult[]> ClaimDueJobsAsync(
        string workerId,
        int batchSize = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the next execution time for a recurring job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the recurring job.</param>
    /// <param name="nextExecution">The next scheduled execution time.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateNextExecutionAsync(
        string jobId,
        DateTimeOffset nextExecution,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves information about all recurring jobs.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Array of recurring job information.</returns>
    Task<RecurringJobInfo[]> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets information about a specific recurring job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the recurring job.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The recurring job information, or null if not found.</returns>
    Task<RecurringJobInfo?> GetAsync(string jobId, CancellationToken ct = default);
}

/// <summary>
/// Result of claiming a due recurring job.
/// </summary>
public sealed record RecurringJobClaimResult(
    string JobId,
    string JobType,
    byte[] Payload,
    string Queue,
    int Priority,
    string CronExpression,
    CronFormat CronFormat,
    TimeZoneInfo? TimeZone,
    MisfirePolicy MisfirePolicy,
    int MaxRetries,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? LastExecution
);

/// <summary>
/// Information about a recurring job.
/// </summary>
public sealed record RecurringJobInfo(
    string JobId,
    string CronExpression,
    string JobType,
    string Queue,
    int Priority,
    TimeZoneInfo? TimeZone,
    MisfirePolicy MisfirePolicy,
    int MaxRetries,
    bool Enabled,
    DateTimeOffset? NextExecution,
    DateTimeOffset? LastExecution,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Cron expression format options.
/// </summary>
public enum CronFormat
{
    /// <summary>
    /// Standard 5-field cron format (minute hour day month day-of-week).
    /// </summary>
    Standard,

    /// <summary>
    /// 6-field cron format including seconds (second minute hour day month day-of-week).
    /// </summary>
    IncludeSeconds
}
