using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Valir.Abstractions;

namespace Valir.Core;

/// <summary>
/// Background service that monitors and schedules recurring jobs.
/// Implements IHostedService for integration with ASP.NET Core and Worker Service hosts.
/// </summary>
/// <remarks>
/// Initializes a new instance of the SchedulerWorker.
/// </remarks>
/// <param name="recurringQueue">The recurring job queue.</param>
/// <param name="jobQueue">The job queue for enqueuing instances.</param>
/// <param name="options">Configuration options.</param>
/// <param name="logger">Optional logger instance.</param>
public sealed class SchedulerWorker(
    IRecurringJobQueue recurringQueue,
    IJobQueue jobQueue,
    IOptions<SchedulerWorkerOptions> options,
    ILogger<SchedulerWorker>? logger = null) : IHostedService, IAsyncDisposable
{
    private readonly IRecurringJobQueue _recurringQueue = recurringQueue ?? throw new ArgumentNullException(nameof(recurringQueue));
    private readonly IJobQueue _jobQueue = jobQueue ?? throw new ArgumentNullException(nameof(jobQueue));
    private readonly ILogger<SchedulerWorker> _logger = logger ?? NullLogger<SchedulerWorker>.Instance;
    private readonly SchedulerWorkerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly string _workerId = $"scheduler-{Guid.CreateVersion7():N}";
    private Timer? _timer;
    private bool _isRunning;

    /// <summary>
    /// Starts the scheduler worker.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken ct)
    {
        if (_isRunning)
        {
            _logger.LogWarning("SchedulerWorker {WorkerId} is already running", _workerId);
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "SchedulerWorker {WorkerId} starting with check interval: {CheckInterval}",
            _workerId, _options.CheckInterval);

        _timer = new Timer(
            callback: _ => _ = CheckAndScheduleAsync(ct),
            state: null,
            dueTime: TimeSpan.Zero,
            period: _options.CheckInterval);

        _isRunning = true;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the scheduler worker.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("SchedulerWorker {WorkerId} stopping", _workerId);

        _timer?.Change(Timeout.Infinite, 0);
        _isRunning = false;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the scheduler worker resources.
    /// </summary>
    /// <returns>A value task representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_timer is not null)
        {
            await _timer.DisposeAsync();
        }
    }

    private async Task CheckAndScheduleAsync(CancellationToken ct)
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            RecurringJobClaimResult[] dueJobs = await _recurringQueue.ClaimDueJobsAsync(
                _workerId,
                _options.BatchSize,
                ct);

            if (dueJobs.Length > 0)
            {
                _logger.LogDebug(
                    "SchedulerWorker {WorkerId} claimed {Count} due recurring jobs",
                    _workerId, dueJobs.Length);
            }

            foreach (RecurringJobClaimResult job in dueJobs)
            {
                await ProcessDueJobAsync(job, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal cancellation, don't log as error
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for due recurring jobs in SchedulerWorker {WorkerId}", _workerId);
        }
    }

    private async Task ProcessDueJobAsync(RecurringJobClaimResult job, CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        try
        {
            // Check if this is a misfire (scheduled time is significantly in the past)
            if (job.ScheduledAt < now.AddMinutes(-1))
            {
                await HandleMisfireAsync(job, ct);
            }
            else
            {
                // Normal execution - enqueue job instance
                await EnqueueJobInstanceAsync(job, ct);
            }

            // Calculate and update next execution
            DateTimeOffset? nextOccurrence = CalculateNextOccurrence(job, now);
            if (nextOccurrence.HasValue)
            {
                await _recurringQueue.UpdateNextExecutionAsync(job.JobId, nextOccurrence.Value, ct);
                _logger.LogDebug(
                    "Updated next execution for recurring job {JobId} to {NextExecution}",
                    job.JobId, nextOccurrence.Value);
            }
            else
            {
                _logger.LogWarning(
                    "Could not calculate next occurrence for recurring job {JobId}",
                    job.JobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing due recurring job {JobId}", job.JobId);
        }
    }

    private async Task HandleMisfireAsync(RecurringJobClaimResult job, CancellationToken ct)
    {
        _logger.LogWarning(
            "Handling misfire for recurring job {JobId} with policy {MisfirePolicy}. " +
            "Scheduled at: {ScheduledAt}, Now: {Now}",
            job.JobId, job.MisfirePolicy, job.ScheduledAt, DateTimeOffset.UtcNow);

        switch (job.MisfirePolicy)
        {
            case MisfirePolicy.Skip:
                _logger.LogInformation(
                    "Skipped misfired job {JobId} scheduled at {ScheduledAt}",
                    job.JobId, job.ScheduledAt);
                break;

            case MisfirePolicy.FireAll:
                List<DateTimeOffset> missedOccurrences = GetMissedOccurrences(job);
                _logger.LogInformation(
                    "Firing {Count} missed occurrences for job {JobId}",
                    missedOccurrences.Count, job.JobId);

                foreach (DateTimeOffset occurrence in missedOccurrences)
                {
                    await EnqueueJobInstanceAsync(job, ct, occurrence);
                }
                break;

            case MisfirePolicy.FireOnce:
                await EnqueueJobInstanceAsync(job, ct);
                break;

            case MisfirePolicy.FireNow:
                await EnqueueJobInstanceAsync(job, ct, DateTimeOffset.UtcNow);
                break;

            default:
                _logger.LogWarning("Unknown misfire policy {Policy} for job {JobId}", job.MisfirePolicy, job.JobId);
                await EnqueueJobInstanceAsync(job, ct);
                break;
        }
    }

    private async Task EnqueueJobInstanceAsync(
        RecurringJobClaimResult job,
        CancellationToken ct,
        DateTimeOffset? scheduledAt = null)
    {
        string instanceId = await _jobQueue.EnqueueAsync(
            job.JobType,
            job.Payload,
            delay: null,
            priority: job.Priority,
            ct: ct);

        _logger.LogInformation(
            "Enqueued recurring job instance {InstanceId} for {JobId} (type: {JobType})",
            instanceId, job.JobId, job.JobType);
    }

    private List<DateTimeOffset> GetMissedOccurrences(RecurringJobClaimResult job)
    {
        List<DateTimeOffset> occurrences = [];
        DateTimeOffset lastExecution = job.LastExecution ?? job.ScheduledAt.AddMonths(-1); // Default to 1 month ago if no history
        DateTimeOffset now = DateTimeOffset.UtcNow;

        try
        {
            Cronos.CronFormat cronFormat = job.CronFormat == CronFormat.IncludeSeconds
                ? Cronos.CronFormat.IncludeSeconds
                : Cronos.CronFormat.Standard;

            Cronos.CronExpression expression = Cronos.CronExpression.Parse(job.CronExpression, cronFormat);
            TimeZoneInfo timeZone = job.TimeZone ?? TimeZoneInfo.Utc;

            DateTime? occurrence = expression.GetNextOccurrence(lastExecution.UtcDateTime, timeZone);
            while (occurrence.HasValue && occurrence.Value < now.UtcDateTime)
            {
                occurrences.Add(new DateTimeOffset(occurrence.Value, timeZone.GetUtcOffset(occurrence.Value)));
                occurrence = expression.GetNextOccurrence(occurrence.Value, timeZone);

                // Safety limit to prevent excessive job creation
                if (occurrences.Count >= 100)
                {
                    _logger.LogWarning(
                        "Reached maximum misfire occurrence limit (100) for job {JobId}",
                        job.JobId);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating missed occurrences for job {JobId}", job.JobId);
        }

        return occurrences;
    }

    private DateTimeOffset? CalculateNextOccurrence(RecurringJobClaimResult job, DateTimeOffset from)
    {
        try
        {
            Cronos.CronFormat cronFormat = job.CronFormat == CronFormat.IncludeSeconds
                ? Cronos.CronFormat.IncludeSeconds
                : Cronos.CronFormat.Standard;

            Cronos.CronExpression expression = Cronos.CronExpression.Parse(job.CronExpression, cronFormat);
            TimeZoneInfo timeZone = job.TimeZone ?? TimeZoneInfo.Utc;

            DateTime? next = expression.GetNextOccurrence(from.UtcDateTime, timeZone);

            if (next.HasValue)
            {
                return new DateTimeOffset(next.Value, timeZone.GetUtcOffset(next.Value));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating next occurrence for job {JobId}", job.JobId);
        }

        return null;
    }
}

/// <summary>
/// Configuration options for the SchedulerWorker.
/// </summary>
public sealed class SchedulerWorkerOptions
{
    /// <summary>
    /// Interval between checks for due recurring jobs. Default: 10 seconds.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of jobs to process per check. Default: 10.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Number of concurrent scheduler workers. Default: 1.
    /// </summary>
    public int Concurrency { get; set; } = 1;
}
