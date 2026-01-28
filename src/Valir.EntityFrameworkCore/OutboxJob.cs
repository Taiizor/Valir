namespace Valir.EntityFrameworkCore;

/// <summary>
/// Entity representing a job in the outbox table.
/// Allows atomic job creation within application transactions.
/// </summary>
public class OutboxJob
{
    /// <summary>
    /// Unique identifier for the outbox entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Unique job identifier (will become JobEnvelope.Id).
    /// </summary>
    public string JobId { get; set; } = Guid.CreateVersion7().ToString("N");

    /// <summary>
    /// Job type for handler routing.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Serialized payload as Base64.
    /// </summary>
    public required string PayloadBase64 { get; set; }

    /// <summary>
    /// Job priority (0 = default, higher = faster).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Optional idempotency key for deduplication.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// When this job was added to the outbox.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether this job has been successfully pushed to Redis.
    /// </summary>
    public bool IsProcessed { get; set; }

    /// <summary>
    /// When this job was pushed to Redis (null if not yet processed).
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>
    /// Number of processing attempts.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Last error message if processing failed.
    /// </summary>
    public string? LastError { get; set; }
}
