using System.Diagnostics;

namespace Valir.AspNet;

/// <summary>
/// OpenTelemetry ActivitySource for Valir distributed tracing.
/// Provides Activity instrumentation for job queue operations.
/// </summary>
public static class ValirTelemetry
{
    /// <summary>
    /// ActivitySource name for Valir traces.
    /// </summary>
    public const string SourceName = "Valir";

    /// <summary>
    /// The ActivitySource used for creating Valir activities.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    /// <summary>
    /// Start an activity for enqueueing a job.
    /// </summary>
    /// <param name="jobType">The job type being enqueued.</param>
    /// <param name="jobId">Optional job ID if already known.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartEnqueue(string jobType, string? jobId = null)
    {
        Activity? activity = Source.StartActivity("valir.job.enqueue", ActivityKind.Producer);
        activity?.SetTag("valir.job.type", jobType);
        if (jobId != null)
        {
            activity?.SetTag("valir.job.id", jobId);
        }

        return activity;
    }

    /// <summary>
    /// Start an activity for publishing an event.
    /// </summary>
    /// <param name="topic">The topic the event is published to.</param>
    /// <param name="eventId">Optional event ID if already known.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartPublish(string topic, string? eventId = null)
    {
        Activity? activity = Source.StartActivity("valir.event.publish", ActivityKind.Producer);
        activity?.SetTag("valir.event.topic", topic);
        if (eventId != null)
        {
            activity?.SetTag("valir.event.id", eventId);
        }

        return activity;
    }

    /// <summary>
    /// Start an activity for claiming a job from the queue.
    /// </summary>
    /// <param name="workerId">The worker ID claiming the job.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartClaim(string workerId)
    {
        Activity? activity = Source.StartActivity("valir.job.claim", ActivityKind.Consumer);
        activity?.SetTag("valir.worker.id", workerId);
        return activity;
    }

    /// <summary>
    /// Start an activity for executing a job.
    /// </summary>
    /// <param name="jobId">The job ID being executed.</param>
    /// <param name="jobType">The job type.</param>
    /// <param name="workerId">The worker ID executing the job.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartExecute(string jobId, string jobType, string workerId)
    {
        Activity? activity = Source.StartActivity("valir.job.execute", ActivityKind.Internal);
        activity?.SetTag("valir.job.id", jobId);
        activity?.SetTag("valir.job.type", jobType);
        activity?.SetTag("valir.worker.id", workerId);
        return activity;
    }

    /// <summary>
    /// Start an activity for completing a job.
    /// </summary>
    /// <param name="jobId">The job ID being completed.</param>
    /// <returns>The started activity, or null if not sampled.</returns>
    public static Activity? StartComplete(string jobId)
    {
        Activity? activity = Source.StartActivity("valir.job.complete", ActivityKind.Internal);
        activity?.SetTag("valir.job.id", jobId);
        return activity;
    }

    /// <summary>
    /// Record an exception on an activity.
    /// </summary>
    /// <param name="activity">The activity to record the exception on.</param>
    /// <param name="ex">The exception that occurred.</param>
    public static void RecordException(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            ["exception.type"] = ex.GetType().FullName,
            ["exception.message"] = ex.Message,
            ["exception.stacktrace"] = ex.StackTrace
        }));
    }
}
