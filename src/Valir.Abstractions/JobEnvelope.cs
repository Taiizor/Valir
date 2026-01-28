namespace Valir.Abstractions;

/// <summary>
/// Represents a job envelope containing all metadata for a background job.
/// </summary>
/// <param name="Id">Unique identifier for this job instance.</param>
/// <param name="Type">The job type name used for handler routing.</param>
/// <param name="Payload">Serialized job payload as bytes.</param>
/// <param name="Attempts">Current attempt count (starts at 0, incremented on each claim).</param>
/// <param name="MaxAttempts">Maximum retry attempts before moving to dead-letter.</param>
/// <param name="CreatedAt">Timestamp when the job was enqueued.</param>
/// <param name="VisibilityTimeout">Duration for which the job is invisible after being claimed.</param>
/// <param name="IdempotencyKey">Optional key for at-least-once deduplication.</param>
/// <param name="Priority">Job priority (0 = default, higher = faster processing).</param>
public sealed record JobEnvelope(
    string Id,
    string Type,
    byte[] Payload,
    int Attempts,
    int MaxAttempts,
    DateTimeOffset CreatedAt,
    TimeSpan VisibilityTimeout,
    string? IdempotencyKey = null,
    int Priority = 0
);
