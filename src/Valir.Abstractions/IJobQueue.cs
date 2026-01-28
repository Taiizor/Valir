namespace Valir.Abstractions;

/// <summary>
/// Interface for enqueueing and managing background jobs.
/// Strictly Redis-backed for reliability and performance.
/// </summary>
public interface IJobQueue
{
    /// <summary>
    /// Enqueue a single job with optional delay and priority.
    /// </summary>
    /// <param name="type">Job type name for handler routing.</param>
    /// <param name="payload">Serialized job payload.</param>
    /// <param name="delay">Optional delay before job becomes visible.</param>
    /// <param name="priority">Job priority (0 = default, higher = faster).</param>
    /// <param name="idempotencyKey">Optional key for deduplication.</param>
    /// <returns>The generated job ID.</returns>
    Task<string> EnqueueAsync(
        string type,
        byte[] payload,
        TimeSpan? delay = null,
        int priority = 0,
        string? idempotencyKey = null);

    /// <summary>
    /// Batch enqueue multiple jobs for high-throughput scenarios.
    /// Uses Redis pipelining for performance (50x-100x faster than individual calls).
    /// </summary>
    /// <param name="jobs">Collection of job definitions.</param>
    /// <param name="priority">Priority for all jobs in the batch.</param>
    /// <returns>Array of generated job IDs.</returns>
    Task<string[]> EnqueueBatchAsync(
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs,
        int priority = 0);

    /// <summary>
    /// Claim the next available job for processing.
    /// </summary>
    /// <param name="workerId">Unique identifier of the claiming worker.</param>
    /// <param name="claimTimeout">Duration for which the job is locked.</param>
    /// <returns>Job envelope or null if no jobs available.</returns>
    Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout);

    /// <summary>
    /// Mark a job as successfully completed.
    /// </summary>
    /// <param name="jobId">The job to complete.</param>
    Task CompleteAsync(string jobId);

    /// <summary>
    /// Mark a job as failed, scheduling retry or dead-letter.
    /// </summary>
    /// <param name="jobId">The job that failed.</param>
    /// <param name="reason">Failure reason for logging.</param>
    Task FailAsync(string jobId, string reason);

    /// <summary>
    /// Release a job back to the queue (e.g., during graceful shutdown).
    /// </summary>
    /// <param name="jobId">The job to release.</param>
    /// <param name="delay">Optional delay before the job becomes visible again.</param>
    Task ReleaseAsync(string jobId, TimeSpan? delay = null);
}
