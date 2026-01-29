using Valir.Core;

namespace Valir.AspNet;

/// <summary>
/// Adapter that implements IValirMetrics by delegating to the static ValirMetrics class.
/// This allows the Valir.AspNet metrics implementation to be used via dependency injection.
/// </summary>
public sealed class ValirMetricsAdapter : IValirMetrics
{
    /// <inheritdoc />
    public void RecordJobClaimed(string workerId, string jobType)
    {
        ValirMetrics.RecordJobClaimed(workerId, jobType);
    }

    /// <inheritdoc />
    public void RecordJobCompleted(string jobType, int attempts)
    {
        ValirMetrics.RecordJobCompleted(jobType, attempts);
    }

    /// <inheritdoc />
    public void RecordJobFailed(string jobType, bool willRetry, string? exceptionType = null)
    {
        ValirMetrics.RecordJobFailed(jobType, willRetry, exceptionType);
    }

    /// <inheritdoc />
    public void RecordJobRetried(string jobType, int attemptNumber)
    {
        ValirMetrics.RecordJobRetried(jobType, attemptNumber);
    }

    /// <inheritdoc />
    public void RecordJobDeadLettered(string jobType, int totalAttempts)
    {
        ValirMetrics.RecordJobDeadLettered(jobType, totalAttempts);
    }

    /// <inheritdoc />
    public void RecordProcessingDuration(TimeSpan duration, string jobType, bool success)
    {
        ValirMetrics.RecordProcessingDuration(duration, jobType, success);
    }

    /// <inheritdoc />
    public void RecordJobWaitTime(TimeSpan waitTime, string jobType)
    {
        ValirMetrics.RecordJobWaitTime(waitTime, jobType);
    }

    /// <inheritdoc />
    public void RecordPayloadSize(int sizeBytes, string jobType)
    {
        ValirMetrics.RecordPayloadSize(sizeBytes, jobType);
    }

    /// <inheritdoc />
    public void IncrementActiveWorkers()
    {
        ValirMetrics.IncrementActiveWorkers();
    }

    /// <inheritdoc />
    public void DecrementActiveWorkers()
    {
        ValirMetrics.DecrementActiveWorkers();
    }

    /// <inheritdoc />
    public void IncrementActiveJobs()
    {
        ValirMetrics.IncrementActiveJobs();
    }

    /// <inheritdoc />
    public void DecrementActiveJobs()
    {
        ValirMetrics.DecrementActiveJobs();
    }

    /// <inheritdoc />
    public void SetQueueDepth(long depth)
    {
        ValirMetrics.SetQueueDepth(depth);
    }

    /// <inheritdoc />
    public IProcessingTimer StartProcessingTimer(string jobType)
    {
        return new ProcessingTimerAdapter(ValirMetrics.StartProcessingTimer(jobType));
    }
}

/// <summary>
/// Adapter for the ProcessingTimer struct to implement IProcessingTimer.
/// </summary>
internal readonly struct ProcessingTimerAdapter : IProcessingTimer
{
    private readonly ValirMetrics.ProcessingTimer _inner;

    public ProcessingTimerAdapter(ValirMetrics.ProcessingTimer inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public IProcessingTimer MarkSuccess()
    {
        return new ProcessingTimerAdapter(_inner.MarkSuccess());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _inner.Dispose();
    }
}
