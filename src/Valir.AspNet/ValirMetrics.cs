using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Valir.AspNet;

/// <summary>
/// OpenTelemetry Metrics for Valir job queue operations.
/// Provides counters, histograms, and gauges for monitoring job processing.
/// </summary>
public static class ValirMetrics
{
    /// <summary>
    /// Meter name for Valir metrics.
    /// </summary>
    public const string MeterName = "Valir";

    /// <summary>
    /// The Meter instance used for creating Valir metrics.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    // Counters
    private static readonly Counter<long> _jobsEnqueued = Meter.CreateCounter<long>(
        "valir.jobs.enqueued",
        unit: "{job}",
        description: "Total number of jobs enqueued");

    private static readonly Counter<long> _jobsClaimed = Meter.CreateCounter<long>(
        "valir.jobs.claimed",
        unit: "{job}",
        description: "Total number of jobs claimed by workers");

    private static readonly Counter<long> _jobsCompleted = Meter.CreateCounter<long>(
        "valir.jobs.completed",
        unit: "{job}",
        description: "Total number of jobs completed successfully");

    private static readonly Counter<long> _jobsFailed = Meter.CreateCounter<long>(
        "valir.jobs.failed",
        unit: "{job}",
        description: "Total number of jobs that failed");

    private static readonly Counter<long> _jobsRetried = Meter.CreateCounter<long>(
        "valir.jobs.retried",
        unit: "{job}",
        description: "Total number of jobs scheduled for retry");

    private static readonly Counter<long> _jobsDeadLettered = Meter.CreateCounter<long>(
        "valir.jobs.dead_lettered",
        unit: "{job}",
        description: "Total number of jobs moved to dead letter queue");

    // Histograms
    private static readonly Histogram<double> _jobProcessingDuration = Meter.CreateHistogram<double>(
        "valir.jobs.processing.duration",
        unit: "s",
        description: "Duration of job processing in seconds");

    private static readonly Histogram<double> _jobWaitTime = Meter.CreateHistogram<double>(
        "valir.jobs.wait_time",
        unit: "s",
        description: "Time between job enqueue and claim in seconds");

    private static readonly Histogram<int> _jobPayloadSize = Meter.CreateHistogram<int>(
        "valir.jobs.payload.size",
        unit: "By",
        description: "Size of job payloads in bytes");

    private static readonly Histogram<int> _jobRetryCount = Meter.CreateHistogram<int>(
        "valir.jobs.retry.count",
        unit: "{retry}",
        description: "Number of retries before completion or dead-lettering");

    // Observable Gauges
    private static long _activeWorkers = 0;
    private static long _activeJobs = 0;
    private static long _queueDepth = 0;

    private static readonly ObservableGauge<long> _activeWorkersGauge = Meter.CreateObservableGauge(
        "valir.workers.active",
        () => Interlocked.Read(ref _activeWorkers),
        unit: "{worker}",
        description: "Number of active worker runtimes");

    private static readonly ObservableGauge<long> _activeJobsGauge = Meter.CreateObservableGauge(
        "valir.jobs.active",
        () => Interlocked.Read(ref _activeJobs),
        unit: "{job}",
        description: "Number of jobs currently being processed");

    private static readonly ObservableGauge<long> _queueDepthGauge = Meter.CreateObservableGauge(
        "valir.queue.depth",
        () => Interlocked.Read(ref _queueDepth),
        unit: "{job}",
        description: "Current depth of the job queue");

    /// <summary>
    /// Record a job being enqueued.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <param name="priority">The job priority.</param>
    public static void RecordJobEnqueued(string jobType, int priority = 0)
    {
        TagList tags = new(
        [
            new KeyValuePair<string, object?>("job.type", jobType),
            new KeyValuePair<string, object?>("job.priority", priority)
        ]);
        _jobsEnqueued.Add(1, tags);
    }

    /// <summary>
    /// Record a job being claimed by a worker.
    /// </summary>
    /// <param name="workerId">The worker ID.</param>
    /// <param name="jobType">The type of job.</param>
    public static void RecordJobClaimed(string workerId, string jobType)
    {
        TagList tags = new(
        [
            new KeyValuePair<string, object?>("worker.id", workerId),
            new KeyValuePair<string, object?>("job.type", jobType)
        ]);
        _jobsClaimed.Add(1, tags);
    }

    /// <summary>
    /// Record a job being completed successfully.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <param name="attempts">Number of attempts before completion.</param>
    public static void RecordJobCompleted(string jobType, int attempts)
    {
        TagList tags = new(
        [
            new KeyValuePair<string, object?>("job.type", jobType)
        ]);
        _jobsCompleted.Add(1, tags);
        _jobRetryCount.Record(attempts, tags);
    }

    /// <summary>
    /// Record a job failure.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <param name="willRetry">Whether the job will be retried.</param>
    /// <param name="exceptionType">The type of exception (optional).</param>
    public static void RecordJobFailed(string jobType, bool willRetry, string? exceptionType = null)
    {
        TagList tags = new(
        [
            new KeyValuePair<string, object?>("job.type", jobType),
            new KeyValuePair<string, object?>("will_retry", willRetry)
        ]);

        if (!string.IsNullOrEmpty(exceptionType))
        {
            tags.Add(new KeyValuePair<string, object?>("exception.type", exceptionType));
        }

        _jobsFailed.Add(1, tags);
    }

    /// <summary>
    /// Record a job being scheduled for retry.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <param name="attemptNumber">The current attempt number.</param>
    public static void RecordJobRetried(string jobType, int attemptNumber)
    {
        TagList tags = new(
        [
            new KeyValuePair<string, object?>("job.type", jobType),
            new KeyValuePair<string, object?>("attempt.number", attemptNumber)
        ]);
        _jobsRetried.Add(1, tags);
    }

    /// <summary>
    /// Record a job being moved to the dead letter queue.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <param name="totalAttempts">Total number of attempts made.</param>
    public static void RecordJobDeadLettered(string jobType, int totalAttempts)
    {
        TagList tags = new(
        [
            new KeyValuePair<string, object?>("job.type", jobType),
            new KeyValuePair<string, object?>("total.attempts", totalAttempts)
        ]);
        _jobsDeadLettered.Add(1, tags);
    }

    /// <summary>
    /// Record the duration of job processing.
    /// </summary>
    /// <param name="duration">The processing duration.</param>
    /// <param name="jobType">The type of job.</param>
    /// <param name="success">Whether the job succeeded.</param>
    public static void RecordProcessingDuration(TimeSpan duration, string jobType, bool success)
    {
        TagList tags = new(
        [
            new KeyValuePair<string, object?>("job.type", jobType),
            new KeyValuePair<string, object?>("success", success)
        ]);
        _jobProcessingDuration.Record(duration.TotalSeconds, tags);
    }

    /// <summary>
    /// Record the wait time between enqueue and claim.
    /// </summary>
    /// <param name="waitTime">The wait time.</param>
    /// <param name="jobType">The type of job.</param>
    public static void RecordJobWaitTime(TimeSpan waitTime, string jobType)
    {
        TagList tags = new(
        [
            new KeyValuePair<string, object?>("job.type", jobType)
        ]);
        _jobWaitTime.Record(waitTime.TotalSeconds, tags);
    }

    /// <summary>
    /// Record the size of a job payload.
    /// </summary>
    /// <param name="sizeBytes">The payload size in bytes.</param>
    /// <param name="jobType">The type of job.</param>
    public static void RecordPayloadSize(int sizeBytes, string jobType)
    {
        TagList tags = new(
        [
            new KeyValuePair<string, object?>("job.type", jobType)
        ]);
        _jobPayloadSize.Record(sizeBytes, tags);
    }

    /// <summary>
    /// Increment the active workers count.
    /// </summary>
    public static void IncrementActiveWorkers()
    {
        Interlocked.Increment(ref _activeWorkers);
    }

    /// <summary>
    /// Decrement the active workers count.
    /// </summary>
    public static void DecrementActiveWorkers()
    {
        Interlocked.Decrement(ref _activeWorkers);
    }

    /// <summary>
    /// Increment the active jobs count.
    /// </summary>
    public static void IncrementActiveJobs()
    {
        Interlocked.Increment(ref _activeJobs);
    }

    /// <summary>
    /// Decrement the active jobs count.
    /// </summary>
    public static void DecrementActiveJobs()
    {
        Interlocked.Decrement(ref _activeJobs);
    }

    /// <summary>
    /// Set the current queue depth.
    /// </summary>
    /// <param name="depth">The queue depth.</param>
    public static void SetQueueDepth(long depth)
    {
        Interlocked.Exchange(ref _queueDepth, depth);
    }

    /// <summary>
    /// Create a timer to measure job processing duration.
    /// </summary>
    /// <param name="jobType">The type of job.</param>
    /// <returns>A disposable timer that records duration on dispose.</returns>
    public static ProcessingTimer StartProcessingTimer(string jobType)
    {
        return new ProcessingTimer(jobType);
    }

    /// <summary>
    /// Disposable timer for measuring job processing duration.
    /// </summary>
    public readonly struct ProcessingTimer : IDisposable
    {
        private readonly string _jobType;
        private readonly long _startTimestamp;
        private readonly bool _success;

        /// <summary>
        /// Initializes a new processing timer.
        /// </summary>
        /// <param name="jobType">The job type.</param>
        public ProcessingTimer(string jobType)
        {
            _jobType = jobType;
            _startTimestamp = Stopwatch.GetTimestamp();
            _success = false;
        }

        private ProcessingTimer(string jobType, long startTimestamp, bool success)
        {
            _jobType = jobType;
            _startTimestamp = startTimestamp;
            _success = success;
        }

        /// <summary>
        /// Mark the job as successfully completed.
        /// </summary>
        /// <returns>A new timer marked as successful.</returns>
        public ProcessingTimer MarkSuccess()
        {
            return new ProcessingTimer(_jobType, _startTimestamp, true);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            TimeSpan duration = Stopwatch.GetElapsedTime(_startTimestamp);
            RecordProcessingDuration(duration, _jobType, _success);
        }
    }
}
