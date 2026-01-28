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
public class OutboxProcessor<TContext> : BackgroundService where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxProcessorOptions _options;
    private readonly ILogger<OutboxProcessor<TContext>> _logger;

    /// <summary>
    /// Initializes a new instance of the OutboxProcessor.
    /// </summary>
    /// <param name="scopeFactory">Service scope factory for creating DbContexts.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        OutboxProcessorOptions options,
        ILogger<OutboxProcessor<TContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
                await CleanupProcessedJobsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error processing outbox");
            }

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }

        _logger.LogInformation("Outbox processor stopped");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
        IJobQueue? redisQueue = scope.ServiceProvider.GetService<IJobQueue>();

        if (redisQueue is null)
        {
            _logger.LogWarning("No IJobQueue registered, skipping outbox processing");
            return;
        }

        // Get pending jobs
        List<OutboxJob> pendingJobs = await context.Set<OutboxJob>()
            .Where(j => !j.IsProcessed && j.Attempts < _options.MaxRetryAttempts && j.CreatedAt <= DateTimeOffset.UtcNow)
            .OrderBy(j => j.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (pendingJobs.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processing {Count} outbox jobs", pendingJobs.Count);

        // Batch enqueue to Redis
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobsToEnqueue = pendingJobs.Select(j => (
            type: j.Type,
            payload: Convert.FromBase64String(j.PayloadBase64),
            idempotencyKey: j.IdempotencyKey
        ));

        try
        {
            await redisQueue.EnqueueBatchAsync(jobsToEnqueue, pendingJobs.First().Priority);

            // Mark as processed
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (OutboxJob? job in pendingJobs)
            {
                job.IsProcessed = true;
                job.ProcessedAt = now;
            }

            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully pushed {Count} jobs from outbox to Redis", pendingJobs.Count);
        }
        catch (Exception ex)
        {
            // Mark failed attempt
            foreach (OutboxJob? job in pendingJobs)
            {
                job.Attempts++;
                job.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            }

            await context.SaveChangesAsync(ct);
            _logger.LogWarning(ex, "Failed to push outbox jobs to Redis, will retry");
        }
    }

    private async Task CleanupProcessedJobsAsync(CancellationToken ct)
    {
        if (_options.RetentionPeriod is null)
        {
            return;
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();

        DateTimeOffset cutoff = DateTimeOffset.UtcNow - _options.RetentionPeriod.Value;

        int deleted = await context.Set<OutboxJob>()
            .Where(j => j.IsProcessed && j.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} processed outbox jobs", deleted);
        }
    }
}
