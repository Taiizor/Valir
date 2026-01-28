namespace Valir.Core;

/// <summary>
/// Job state machine states.
/// </summary>
public enum JobState
{
    /// <summary>Job is waiting to be claimed.</summary>
    Waiting,

    /// <summary>Job is currently being processed.</summary>
    Active,

    /// <summary>Job is scheduled for retry after failure.</summary>
    Retry,

    /// <summary>Job has been completed successfully.</summary>
    Completed,

    /// <summary>Job has failed all retries and is dead-lettered.</summary>
    Dead
}
