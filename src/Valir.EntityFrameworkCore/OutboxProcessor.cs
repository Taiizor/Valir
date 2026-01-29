using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Valir.Abstractions;

namespace Valir.EntityFrameworkCore;

/// <summary>
/// Configuration options for the outbox processor.
/// </summary>
public class OutboxProcessorOptions
{
    /// <summary>
    /// How often to poll for pending outbox jobs.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum jobs to process in a single batch.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum retry attempts before giving up.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// How long to keep processed jobs before cleanup (null = never delete).
    /// </summary>
    public TimeSpan? RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// Background service that processes the outbox and pushes jobs to Redis.
/// </summary>
/// <remarks>
/// Initializes a new instance of the OutboxProcessor.
/// </remarks>
/// <param name="scopeFactory">Service scope factory for creating DbContexts.</param>
/// <param name="options">Configuration options.</param>
/// <param name="logger">Logger instance.</param>
public class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    OutboxProcessorOptions options,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService where TContext : DbContext
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
                await CleanupProcessedJobsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error processing outbox");
            }

            await Task.Delay(options.PollingInterval, stoppingToken);
        }

        logger.LogInformation("Outbox processor stopped");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
        IJobQueue? redisQueue = scope.ServiceProvider.GetService<IJobQueue>();

        if (redisQueue is null)
        {
            logger.LogWarning("No IJobQueue registered, skipping outbox processing");
            return;
        }

        // Get pending jobs
        List<OutboxJob> pendingJobs = await context.Set<OutboxJob>()
            .Where(j => !j.IsProcessed && j.Attempts < options.MaxRetryAttempts && j.CreatedAt <= DateTimeOffset.UtcNow)
            .OrderBy(j => j.CreatedAt)
            .Take(options.BatchSize)
            .ToListAsync(ct);

        if (pendingJobs.Count == 0)
        {
            return;
        }

        logger.LogDebug("Processing {Count} outbox jobs", pendingJobs.Count);

        // Batch enqueue to Redis
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobsToEnqueue = pendingJobs.Select(j => (
            type: j.Type,
            payload: Convert.FromBase64String(j.PayloadBase64),
            idempotencyKey: j.IdempotencyKey
        ));

        try
        {
            await redisQueue.EnqueueBatchAsync(jobsToEnqueue, pendingJobs.First().Priority, ct);

            // Mark as processed
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (OutboxJob? job in pendingJobs)
            {
                job.IsProcessed = true;
                job.ProcessedAt = now;
            }

            await context.SaveChangesAsync(ct);
            logger.LogInformation("Successfully pushed {Count} jobs from outbox to Redis", pendingJobs.Count);
        }
        catch (Exception ex)
        {
            // Mark failed attempt - sanitize error message to remove stack traces
            string sanitizedError = SanitizeErrorMessage(ex.Message);
            foreach (OutboxJob? job in pendingJobs)
            {
                job.Attempts++;
                job.LastError = sanitizedError.Length > 2000 ? sanitizedError[..2000] : sanitizedError;
            }

            await context.SaveChangesAsync(ct);
            logger.LogWarning(ex, "Failed to push outbox jobs to Redis, will retry");
        }
    }

    private async Task CleanupProcessedJobsAsync(CancellationToken ct)
    {
        if (options.RetentionPeriod is null)
        {
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();

        DateTimeOffset cutoff = DateTimeOffset.UtcNow - options.RetentionPeriod.Value;

        int deleted = await context.Set<OutboxJob>()
            .Where(j => j.IsProcessed && j.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            logger.LogInformation("Cleaned up {Count} processed outbox jobs", deleted);
        }
    }

    /// <summary>
    /// Sanitizes error messages to remove stack traces and sensitive information.
    /// </summary>
    /// <param name="errorMessage">The original error message.</param>
    /// <returns>A sanitized error message safe for storage.</returns>
    private static string SanitizeErrorMessage(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
        {
            return string.Empty;
        }

        // Find the first line break that typically precedes stack trace
        int stackTraceStart = errorMessage.IndexOf("\n   at ", StringComparison.Ordinal);
        if (stackTraceStart > 0)
        {
            return errorMessage[..stackTraceStart].Trim();
        }

        // Also check for Windows-style line endings
        stackTraceStart = errorMessage.IndexOf("\r\n   at ", StringComparison.Ordinal);
        if (stackTraceStart > 0)
        {
            return errorMessage[..stackTraceStart].Trim();
        }

        return errorMessage;
    }
}
