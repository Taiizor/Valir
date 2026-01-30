namespace Valir.Abstractions;

/// <summary>
/// Policy for handling missed (misfired) recurring job executions.
/// </summary>
public enum MisfirePolicy
{
    /// <summary>
    /// Skip missed executions and wait for the next scheduled time.
    /// </summary>
    Skip,

    /// <summary>
    /// Fire a separate job instance for each missed execution.
    /// Use with caution - can create many jobs if the scheduler was down for a while.
    /// </summary>
    FireAll,

    /// <summary>
    /// Fire a single job instance for all missed executions (default).
    /// </summary>
    FireOnce,

    /// <summary>
    /// Fire immediately, then resume normal schedule.
    /// </summary>
    FireNow
}
