namespace Valir.Core;

/// <summary>
/// Interface for Valir metrics operations.
/// Provides methods for tracking job processing metrics.
/// </summary>
public interface IValirMetrics
{
    /// <summary>
    /// Record a job being claimed by a worker.
    /// </summary>
    /// <param name="workerId">The worker ID.</param>
    /// <param name="jobType">The type of job.</param>
    void RecordJobClaimed(string workerId, string jobType);

    /// <summary>
    /// Record a job being completed successfully.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <param name="attempts">Number of attempts before completion.</param>
    void RecordJobCompleted(string jobType, int attempts);

    /// <summary>
    /// Record a job failure.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <param name="willRetry">Whether the job will be retried.</param>
    /// <param name="exceptionType">The type of exception (optional).</param>
    void RecordJobFailed(string jobType, bool willRetry, string? exceptionType = null);

    /// <summary>
    /// Record a job being scheduled for retry.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <param name="attemptNumber">The current attempt number.</param>
    void RecordJobRetried(string jobType, int attemptNumber);

    /// <summary>
    /// Record a job being moved to the dead letter queue.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <param name="totalAttempts">Total number of attempts made.</param>
    void RecordJobDeadLettered(string jobType, int totalAttempts);

    /// <summary>
    /// Record the duration of job processing.
    /// </summary>
    /// <param name="duration">The processing duration.</param>
    /// <param name="jobType">The type of job.</param>
    /// <param name="success">Whether the job succeeded.</param>
    void RecordProcessingDuration(TimeSpan duration, string jobType, bool success);

    /// <summary>
    /// Record the wait time between enqueue and claim.
    /// </summary>
    /// <param name="waitTime">The wait time.</param>
    /// <param name="jobType">The type of job.</param>
    void RecordJobWaitTime(TimeSpan waitTime, string jobType);

    /// <summary>
    /// Record the size of a job payload.
    /// </summary>
    /// <param name="sizeBytes">The payload size in bytes.</param>
    /// <param name="jobType">The type of job.</param>
    void RecordPayloadSize(int sizeBytes, string jobType);

    /// <summary>
    /// Increment the active workers count.
    /// </summary>
    void IncrementActiveWorkers();

    /// <summary>
    /// Decrement the active workers count.
    /// </summary>
    void DecrementActiveWorkers();

    /// <summary>
    /// Increment the active jobs count.
    /// </summary>
    void IncrementActiveJobs();

    /// <summary>
    /// Decrement the active jobs count.
    /// </summary>
    void DecrementActiveJobs();

    /// <summary>
    /// Set the current queue depth.
    /// </summary>
    /// <param name="depth">The queue depth.</param>
    void SetQueueDepth(long depth);

    /// <summary>
    /// Create a timer to measure job processing duration.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <returns>A disposable timer that records duration on dispose.</returns>
    IProcessingTimer StartProcessingTimer(string jobType);
}

/// <summary>
/// Interface for a processing timer that records duration on disposal.
/// </summary>
public interface IProcessingTimer : IDisposable
{
    /// <summary>
    /// Mark the job as successfully completed.
    /// </summary>
    /// <returns>The timer marked as successful.</returns>
    IProcessingTimer MarkSuccess();
}
