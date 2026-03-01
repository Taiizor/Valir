using Serilog.Events;
using System.Diagnostics;
using System.Text.Json;
using Valir.Abstractions;
using ILogger = Serilog.ILogger;

namespace Valir.Extensions.Serilog;

/// <summary>
/// Wrapper that provides Serilog logging with Valir job context enrichment.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ValirSerilogLogger"/> class.
/// </remarks>
/// <param name="logger">The Serilog logger.</param>
/// <param name="options">The Serilog options.</param>
/// <param name="enricher">The job context enricher.</param>
public sealed class ValirSerilogLogger(ILogger logger, SerilogOptions options, IJobContextEnricher enricher)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SerilogOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IJobContextEnricher _enricher = enricher ?? throw new ArgumentNullException(nameof(enricher));

    /// <summary>
    /// Initializes a new instance of the <see cref="ValirSerilogLogger"/> class.
    /// </summary>
    /// <param name="logger">The Serilog logger.</param>
    /// <param name="options">The Serilog options.</param>
    /// <param name="enricher">The job context enricher.</param>
    public ValirSerilogLogger(ILogger logger, SerilogOptions options, JobContextEnricher enricher)
        : this(logger, options, (IJobContextEnricher)enricher)
    {
    }

    /// <summary>
    /// Logs the start of a job execution.
    /// </summary>
    /// <param name="jobName">The job name/type.</param>
    /// <param name="context">The job context.</param>
    /// <param name="attempt">The attempt number.</param>
    public void LogJobStart(string jobName, JobContext context, int attempt)
    {
        if (!ShouldLogJobType(jobName))
        {
            return;
        }

        _enricher.SetContext(context, jobName, attempt);

        LogEventLevel level = _options.JobStartLogLevel;
        if (!_logger.IsEnabled(level))
        {
            return;
        }

        _logger.Write(level, "Job {JobName} started (Attempt {Attempt})", jobName, attempt);
    }

    /// <summary>
    /// Logs the successful completion of a job execution.
    /// </summary>
    /// <param name="jobName">The job name/type.</param>
    /// <param name="context">The job context.</param>
    /// <param name="attempt">The attempt number.</param>
    /// <param name="stopwatch">The stopwatch measuring execution time.</param>
    public void LogJobComplete(string jobName, JobContext context, int attempt, Stopwatch stopwatch)
    {
        if (!ShouldLogJobType(jobName))
        {
            _enricher.ClearContext();
            return;
        }

        _enricher.SetContext(context, jobName, attempt);

        LogEventLevel level = _options.JobCompleteLogLevel;
        if (!_logger.IsEnabled(level))
        {
            _enricher.ClearContext();
            return;
        }

        if (_options.IncludeTiming)
        {
            stopwatch.Stop();
            _logger.Write(level, "Job {JobName} completed in {DurationMs}ms (Attempt {Attempt})",
                jobName, stopwatch.ElapsedMilliseconds, attempt);
        }
        else
        {
            _logger.Write(level, "Job {JobName} completed (Attempt {Attempt})", jobName, attempt);
        }

        _enricher.ClearContext();
    }

    /// <summary>
    /// Logs a job failure.
    /// </summary>
    /// <param name="jobName">The job name/type.</param>
    /// <param name="context">The job context.</param>
    /// <param name="attempt">The attempt number.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="stopwatch">The stopwatch measuring execution time.</param>
    public void LogJobFailure(string jobName, JobContext context, int attempt, Exception exception, Stopwatch? stopwatch)
    {
        if (!ShouldLogJobType(jobName))
        {
            _enricher.ClearContext();
            return;
        }

        _enricher.SetContext(context, jobName, attempt);

        LogEventLevel level = _options.JobFailureLogLevel;
        if (!_logger.IsEnabled(level))
        {
            _enricher.ClearContext();
            return;
        }

        if (_options.IncludeTiming && stopwatch is not null)
        {
            stopwatch.Stop();
            _logger.Write(level, exception,
                "Job {JobName} failed after {DurationMs}ms (Attempt {Attempt})",
                jobName, stopwatch.ElapsedMilliseconds, attempt);
        }
        else
        {
            _logger.Write(level, exception,
                "Job {JobName} failed (Attempt {Attempt})", jobName, attempt);
        }

        _enricher.ClearContext();
    }

    /// <summary>
    /// Logs a job retry event.
    /// </summary>
    /// <param name="jobName">The job name/type.</param>
    /// <param name="context">The job context.</param>
    /// <param name="attempt">The attempt number.</param>
    /// <param name="exception">The exception that caused the retry.</param>
    public void LogJobRetry(string jobName, JobContext context, int attempt, Exception exception)
    {
        if (!ShouldLogJobType(jobName))
        {
            return;
        }

        _enricher.SetContext(context, jobName, attempt);

        LogEventLevel level = _options.JobRetryLogLevel;
        if (!_logger.IsEnabled(level))
        {
            return;
        }

        _logger.Write(level, exception,
            "Job {JobName} will be retried (Attempt {Attempt})", jobName, attempt);
    }

    /// <summary>
    /// Logs job payload if enabled.
    /// </summary>
    /// <typeparam name="TJob">The job type.</typeparam>
    /// <param name="jobName">The job name/type.</param>
    /// <param name="context">The job context.</param>
    /// <param name="job">The job payload.</param>
    public void LogJobPayload<TJob>(string jobName, JobContext context, TJob job) where TJob : notnull
    {
        if (!_options.LogJobPayload || !ShouldLogJobType(jobName))
        {
            return;
        }

        _enricher.SetContext(context, jobName, 0);

        try
        {
            string payload = JsonSerializer.Serialize(job);
            if (payload.Length > _options.MaxPayloadLogLength)
            {
                payload = payload[.._options.MaxPayloadLogLength] + "... [truncated]";
            }

            _logger.Debug("Job {JobName} payload: {Payload}", jobName, payload);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to serialize job {JobName} payload for logging", jobName);
        }
    }

    /// <summary>
    /// Gets the underlying Serilog logger for custom logging.
    /// </summary>
    /// <param name="context">The job context for enrichment.</param>
    /// <param name="jobName">The job name/type.</param>
    /// <param name="attempt">The attempt number.</param>
    /// <returns>The Serilog logger with job context.</returns>
    public ILogger ForContext(JobContext context, string jobName, int attempt = 0)
    {
        _enricher.SetContext(context, jobName, attempt);
        return _logger;
    }

    /// <summary>
    /// Clears the current job context.
    /// </summary>
    public void ClearContext()
    {
        _enricher.ClearContext();
    }

    private bool ShouldLogJobType(string jobName)
    {
        return _options.JobTypeFilter?.Invoke(jobName) ?? true;
    }
}
