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
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The generated job ID.</returns>
    /// <exception cref="ArgumentException">Thrown when type is null, empty, or contains invalid characters.</exception>
    /// <exception cref="ArgumentException">Thrown when payload is null or exceeds size limits.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<string> EnqueueAsync(
        string type,
        byte[] payload,
        TimeSpan? delay = null,
        int priority = 0,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    /// <summary>
    /// Batch enqueue multiple jobs for high-throughput scenarios.
    /// Uses Redis pipelining for performance (50x-100x faster than individual calls).
    /// </summary>
    /// <param name="jobs">Collection of job definitions.</param>
    /// <param name="priority">Priority for all jobs in the batch.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Array of generated job IDs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when jobs is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<string[]> EnqueueBatchAsync(
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs,
        int priority = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Claim the next available job for processing.
    /// </summary>
    /// <param name="workerId">Unique identifier of the claiming worker.</param>
    /// <param name="claimTimeout">Duration for which the job is locked.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Job envelope or null if no jobs available.</returns>
    /// <exception cref="ArgumentException">Thrown when workerId is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout, CancellationToken ct = default);

    /// <summary>
    /// Mark a job as successfully completed.
    /// </summary>
    /// <param name="jobId">The job to complete.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous completion operation.</returns>
    /// <exception cref="ArgumentException">Thrown when jobId is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task CompleteAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Mark a job as failed, scheduling retry or dead-letter.
    /// </summary>
    /// <param name="jobId">The job that failed.</param>
    /// <param name="reason">Failure reason for logging.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous failure operation.</returns>
    /// <exception cref="ArgumentException">Thrown when jobId is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task FailAsync(string jobId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Release a job back to the queue (e.g., during graceful shutdown).
    /// </summary>
    /// <param name="jobId">The job to release.</param>
    /// <param name="delay">Optional delay before the job becomes visible again.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous release operation.</returns>
    /// <exception cref="ArgumentException">Thrown when jobId is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task ReleaseAsync(string jobId, TimeSpan? delay = null, CancellationToken ct = default);

    /// <summary>
    /// Atomically extend the lock TTL for a job currently being processed.
    /// </summary>
    /// <param name="jobId">The job to extend the lock for.</param>
    /// <param name="workerId">Unique identifier of the worker holding the lock.</param>
    /// <param name="extension">Duration to extend the lock by.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>True if the lock was extended successfully, false if the lock is not held by the requesting worker or doesn't exist.</returns>
    /// <exception cref="ArgumentException">Thrown when jobId or workerId is null or empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<bool> ExtendLockAsync(string jobId, string workerId, TimeSpan extension, CancellationToken ct = default);
}
