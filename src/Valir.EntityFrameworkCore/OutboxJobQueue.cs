using Microsoft.EntityFrameworkCore;
using Valir.Abstractions;

namespace Valir.EntityFrameworkCore;

/// <summary>
/// Outbox-backed job queue that writes jobs to the database first.
/// Ensures atomicity with application transactions.
/// </summary>
/// <typeparam name="TContext">The DbContext type containing the outbox table.</typeparam>
public class OutboxJobQueue<TContext> : IJobQueue where TContext : DbContext
{
    private readonly TContext _context;
    private readonly IJobQueue? _innerQueue;

    /// <summary>
    /// Create an outbox queue that writes to DB only.
    /// Jobs are pushed to Redis by the OutboxProcessor background service.
    /// </summary>
    public OutboxJobQueue(TContext context)
    {
        _context = context;
        _innerQueue = null;
    }

    /// <summary>
    /// Create an outbox queue with a fallback inner queue.
    /// </summary>
    public OutboxJobQueue(TContext context, IJobQueue innerQueue)
    {
        _context = context;
        _innerQueue = innerQueue;
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync(
        string type,
        byte[] payload,
        TimeSpan? delay = null,
        int priority = 0,
        string? idempotencyKey = null)
    {
        OutboxJob outboxJob = new()
        {
            Type = type,
            PayloadBase64 = Convert.ToBase64String(payload),
            Priority = priority,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTimeOffset.UtcNow + (delay ?? TimeSpan.Zero)
        };

        _context.Set<OutboxJob>().Add(outboxJob);
        // Note: SaveChanges is NOT called here - caller must call it as part of their transaction

        return outboxJob.JobId;
    }

    /// <inheritdoc />
    public async Task<string[]> EnqueueBatchAsync(
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs,
        int priority = 0)
    {
        List<OutboxJob> outboxJobs = jobs.Select(j => new OutboxJob
        {
            Type = j.type,
            PayloadBase64 = Convert.ToBase64String(j.payload),
            Priority = priority,
            IdempotencyKey = j.idempotencyKey,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();

        _context.Set<OutboxJob>().AddRange(outboxJobs);

        return outboxJobs.Select(j => j.JobId).ToArray();
    }

    // These operations delegate to the inner queue (Redis)
    // They are not meant to be used with the outbox pattern

    /// <inheritdoc />
    public Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout)
    {
        return _innerQueue?.ClaimAsync(workerId, claimTimeout) ?? Task.FromResult<JobEnvelope?>(null);
    }

    /// <inheritdoc />
    public Task CompleteAsync(string jobId)
    {
        return _innerQueue?.CompleteAsync(jobId) ?? Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task FailAsync(string jobId, string reason)
    {
        return _innerQueue?.FailAsync(jobId, reason) ?? Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(string jobId, TimeSpan? delay = null)
    {
        return _innerQueue?.ReleaseAsync(jobId, delay) ?? Task.CompletedTask;
    }
}
