namespace Valir.Abstractions;

/// <summary>
/// Configuration options for a recurring job.
/// </summary>
public sealed class RecurringJobOptions
{
    /// <summary>
    /// Cron expression format. Default: Standard (5 fields).
    /// </summary>
    public CronFormat CronFormat { get; set; } = CronFormat.Standard;

    /// <summary>
    /// Time zone for the cron schedule. Default: UTC.
    /// </summary>
    public TimeZoneInfo? TimeZone { get; set; }

    /// <summary>
    /// Target queue for job instances. Default: "default".
    /// </summary>
    public string Queue { get; set; } = "default";

    /// <summary>
    /// Job priority (0 = default, higher = faster). Default: 0.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Policy for handling missed executions. Default: FireOnce.
    /// </summary>
    public MisfirePolicy MisfirePolicy { get; set; } = MisfirePolicy.FireOnce;

    /// <summary>
    /// Maximum retry attempts for failed job instances. Default: 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
