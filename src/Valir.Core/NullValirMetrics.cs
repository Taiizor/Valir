namespace Valir.Core;

/// <summary>
/// Null implementation of IValirMetrics that performs no operations.
/// Used as a default when metrics are not configured.
/// </summary>
public sealed class NullValirMetrics : IValirMetrics
{
    /// <summary>
    /// Singleton instance of NullValirMetrics.
    /// </summary>
    public static readonly NullValirMetrics Instance = new();

    private NullValirMetrics() { }

    /// <inheritdoc />
    public void RecordJobClaimed(string workerId, string jobType) { }

    /// <inheritdoc />
    public void RecordJobCompleted(string jobType, int attempts) { }

    /// <inheritdoc />
    public void RecordJobFailed(string jobType, bool willRetry, string? exceptionType = null) { }

    /// <inheritdoc />
    public void RecordJobRetried(string jobType, int attemptNumber) { }

    /// <inheritdoc />
    public void RecordJobDeadLettered(string jobType, int totalAttempts) { }

    /// <inheritdoc />
    public void RecordProcessingDuration(TimeSpan duration, string jobType, bool success) { }

    /// <inheritdoc />
    public void RecordJobWaitTime(TimeSpan waitTime, string jobType) { }

    /// <inheritdoc />
    public void RecordPayloadSize(int sizeBytes, string jobType) { }

    /// <inheritdoc />
    public void IncrementActiveWorkers() { }

    /// <inheritdoc />
    public void DecrementActiveWorkers() { }

    /// <inheritdoc />
    public void IncrementActiveJobs() { }

    /// <inheritdoc />
    public void DecrementActiveJobs() { }

    /// <inheritdoc />
    public void SetQueueDepth(long depth) { }

    /// <inheritdoc />
    public IProcessingTimer StartProcessingTimer(string jobType)
    {
        return NullProcessingTimer.Instance;
    }
}

/// <summary>
/// Null implementation of IProcessingTimer.
/// </summary>
public sealed class NullProcessingTimer : IProcessingTimer
{
    /// <summary>
    /// Singleton instance of NullProcessingTimer.
    /// </summary>
    public static readonly NullProcessingTimer Instance = new();

    private NullProcessingTimer() { }

    /// <inheritdoc />
    public IProcessingTimer MarkSuccess()
    {
        return this;
    }

    /// <inheritdoc />
    public void Dispose() { }
}
