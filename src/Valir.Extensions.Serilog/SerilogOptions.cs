using Serilog.Events;

namespace Valir.Extensions.Serilog;

/// <summary>
/// Configuration options for Serilog integration with Valir.
/// </summary>
public sealed class SerilogOptions
{
    /// <summary>
    /// Minimum log level for job execution logs. Default: Information.
    /// </summary>
    public LogEventLevel MinimumLogLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Log level for job start events. Default: Information.
    /// </summary>
    public LogEventLevel JobStartLogLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Log level for job completion events. Default: Information.
    /// </summary>
    public LogEventLevel JobCompleteLogLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Log level for job failure events. Default: Error.
    /// </summary>
    public LogEventLevel JobFailureLogLevel { get; set; } = LogEventLevel.Error;

    /// <summary>
    /// Log level for job retry events. Default: Warning.
    /// </summary>
    public LogEventLevel JobRetryLogLevel { get; set; } = LogEventLevel.Warning;

    /// <summary>
    /// Whether to enrich logs with job context (JobId, JobName, WorkerId, etc.). Default: true.
    /// </summary>
    public bool EnrichWithJobContext { get; set; } = true;

    /// <summary>
    /// Whether to log job payload (use with caution - may contain sensitive data). Default: false.
    /// </summary>
    public bool LogJobPayload { get; set; } = false;

    /// <summary>
    /// Maximum length of job payload to log (if LogJobPayload is true). Default: 1000.
    /// </summary>
    public int MaxPayloadLogLength { get; set; } = 1000;

    /// <summary>
    /// Whether to include timing information (duration) in completion logs. Default: true.
    /// </summary>
    public bool IncludeTiming { get; set; } = true;

    /// <summary>
    /// Custom property name for JobId in logs. Default: "JobId".
    /// </summary>
    public string JobIdPropertyName { get; set; } = "JobId";

    /// <summary>
    /// Custom property name for JobName in logs. Default: "JobName".
    /// </summary>
    public string JobNamePropertyName { get; set; } = "JobName";

    /// <summary>
    /// Custom property name for WorkerId in logs. Default: "WorkerId".
    /// </summary>
    public string WorkerIdPropertyName { get; set; } = "WorkerId";

    /// <summary>
    /// Custom property name for Attempt number in logs. Default: "Attempt".
    /// </summary>
    public string AttemptPropertyName { get; set; } = "Attempt";

    /// <summary>
    /// Optional filter to exclude certain job types from logging.
    /// Return true to include the job type, false to exclude.
    /// </summary>
    public Func<string, bool>? JobTypeFilter { get; set; }
}
