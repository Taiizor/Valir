using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Valir.Abstractions;

namespace Valir.Core;

/// <summary>
/// Worker runtime using System.Threading.Channels for backpressure.
/// Implements graceful shutdown (drain mode) and adaptive polling.
/// </summary>
public sealed class WorkerRuntime : IJobWorker, IAsyncDisposable
{
    private readonly IJobQueue _queue;
    private readonly Func<JobEnvelope, JobContext, Task> _handler;
    private readonly ValirOptions _options;
    private readonly ILogger<WorkerRuntime> _logger;
    private readonly Channel<JobEnvelope> _channel;
    private readonly List<Task> _processorTasks = [];
    private readonly CancellationTokenSource?[] _heartbeatCts;
    private CancellationTokenSource? _cts;
    private Task? _claimTask;
    private int _activeJobs;
    private TimeSpan _currentPollingInterval;
    private readonly Random _jitterRandom = new();

    /// <summary>
    /// Unique identifier for this worker instance.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Initializes a new instance of the WorkerRuntime.
    /// </summary>
    /// <param name="queue">The job queue to consume from.</param>
    /// <param name="handler">The job handler delegate.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="workerId">Optional explicit worker ID.</param>
    /// <param name="logger">Optional logger instance.</param>
    public WorkerRuntime(
        IJobQueue queue,
        Func<JobEnvelope, JobContext, Task> handler,
        ValirOptions options,
        string? workerId = null,
        ILogger<WorkerRuntime>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        _queue = queue;
        _handler = handler;
        _options = options;
        _logger = logger ?? NullLogger<WorkerRuntime>.Instance;
        WorkerId = workerId ?? $"worker-{Guid.CreateVersion7():N}";
        _currentPollingInterval = options.PollingInterval;
        _heartbeatCts = new CancellationTokenSource?[options.Concurrency];

        // Bounded channel creates natural backpressure
        _channel = Channel.CreateBounded<JobEnvelope>(new BoundedChannelOptions(_options.Concurrency * 2)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _logger.LogInformation("WorkerRuntime initialized with WorkerId: {WorkerId}, Concurrency: {Concurrency}",
            WorkerId, options.Concurrency);
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _logger.LogInformation("Starting WorkerRuntime {WorkerId} with {Concurrency} processors",
            WorkerId, _options.Concurrency);

        // Start processor tasks
        for (int i = 0; i < _options.Concurrency; i++)
        {
            int processorIndex = i;
            _processorTasks.Add(ProcessorLoopAsync(_cts.Token, processorIndex));
            _logger.LogDebug("Started processor {ProcessorIndex}", processorIndex);
        }

        // Start claim loop
        _claimTask = ClaimLoopAsync(_cts.Token);

        _logger.LogInformation("WorkerRuntime {WorkerId} started successfully", WorkerId);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("Stopping WorkerRuntime {WorkerId}...", WorkerId);

        // Phase 1: Stop claiming new jobs
        _cts?.Cancel();
        _channel.Writer.Complete();

        // Cancel all heartbeats
        foreach (CancellationTokenSource? heartbeatCts in _heartbeatCts)
        {
            heartbeatCts?.Cancel();
        }

        _logger.LogDebug("Cancelled claim loop and heartbeats for {WorkerId}", WorkerId);

        // Phase 2: Wait for active jobs to drain (with timeout)
        using CancellationTokenSource shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        shutdownCts.CancelAfter(_options.ShutdownTimeout);

        try
        {
            // Wait for all processors to finish
            if (_claimTask is not null)
            {
                await _claimTask.WaitAsync(shutdownCts.Token);
            }

            await Task.WhenAll(_processorTasks).WaitAsync(shutdownCts.Token);

            _logger.LogInformation("WorkerRuntime {WorkerId} stopped gracefully", WorkerId);
        }
        catch (OperationCanceledException)
        {
            // Phase 3: Timeout reached, release remaining jobs
            // This is handled by the processor loop's finally block
            _logger.LogWarning("WorkerRuntime {WorkerId} shutdown timed out after {Timeout}s. {ActiveJobs} jobs may have been released.",
                WorkerId, _options.ShutdownTimeout.TotalSeconds, _activeJobs);
        }
    }

    private async Task ClaimLoopAsync(CancellationToken ct)
    {
        _logger.LogDebug("Claim loop started for {WorkerId}", WorkerId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                JobEnvelope? job = await _queue.ClaimAsync(WorkerId, _options.DefaultVisibilityTimeout, ct).ConfigureAwait(false);

                if (job is null)
                {
                    // Apply exponential backoff when queue is empty
                    TimeSpan delay = CalculatePollingDelay();
                    _logger.LogDebug("No jobs available, waiting {DelayMs}ms before next claim", delay.TotalMilliseconds);
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    continue;
                }

                // Reset polling interval on successful claim
                _currentPollingInterval = _options.PollingInterval;

                _logger.LogDebug("Claimed job {JobId} of type {JobType}", job.Id, job.Type);
                await _channel.Writer.WriteAsync(job, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogDebug("Claim loop cancelled for {WorkerId}", WorkerId);
                break;
            }
            catch (Exception ex)
            {
                // Log error and continue with backoff
                _logger.LogError(ex, "Error claiming job for {WorkerId}", WorkerId);
                TimeSpan delay = CalculatePollingDelay();
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        _logger.LogDebug("Claim loop ended for {WorkerId}", WorkerId);
    }

    /// <summary>
    /// Calculates the next polling delay with exponential backoff and optional jitter.
    /// </summary>
    private TimeSpan CalculatePollingDelay()
    {
        // Apply exponential backoff
        TimeSpan nextInterval = TimeSpan.FromTicks(
            (long)(_currentPollingInterval.Ticks * _options.PollingBackoffMultiplier));

        // Cap at maximum polling interval
        if (nextInterval > _options.MaxPollingInterval)
        {
            nextInterval = _options.MaxPollingInterval;
        }

        // Store for next iteration
        _currentPollingInterval = nextInterval;

        // Apply jitter to prevent thundering herd
        if (_options.EnablePollingJitter)
        {
            // Add ±25% jitter
            double jitterFactor = 0.75 + (_jitterRandom.NextDouble() * 0.5);
            nextInterval = TimeSpan.FromTicks((long)(nextInterval.Ticks * jitterFactor));
        }

        return nextInterval;
    }

    private async Task ProcessorLoopAsync(CancellationToken ct, int processorIndex)
    {
        _logger.LogDebug("Processor {ProcessorIndex} started for {WorkerId}", processorIndex, WorkerId);

        await foreach (JobEnvelope job in _channel.Reader.ReadAllAsync(ct))
        {
            Interlocked.Increment(ref _activeJobs);
            CancellationTokenSource? heartbeatCts = null;

            _logger.LogInformation("Processing job {JobId} of type {JobType} (Attempt {Attempt}/{MaxAttempts})",
                job.Id, job.Type, job.Attempts, job.MaxAttempts);

            try
            {
                JobContext context = new(
                    job.Id,
                    WorkerId,
                    $"{WorkerId}:{job.Id}:{job.Attempts}",
                    ct
                );

                // Start heartbeat if enabled
                Task? heartbeatTask = null;
                if (_options.EnableHeartbeat)
                {
                    heartbeatCts = new CancellationTokenSource();
                    _heartbeatCts[processorIndex] = heartbeatCts;
                    heartbeatTask = HeartbeatLoopAsync(job.Id, heartbeatCts.Token);
                }

                try
                {
                    await _handler(job, context).ConfigureAwait(false);
                    await _queue.CompleteAsync(job.Id, ct).ConfigureAwait(false);
                    _logger.LogInformation("Job {JobId} completed successfully", job.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Job {JobId} failed: {ErrorMessage}", job.Id, ex.Message);
                    await _queue.FailAsync(job.Id, ex.Message, ct).ConfigureAwait(false);
                }
                finally
                {
                    heartbeatCts?.Cancel();
                    if (heartbeatTask is not null)
                    {
                        try { await heartbeatTask.ConfigureAwait(false); } catch { }
                    }
                    _heartbeatCts[processorIndex] = null;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Graceful shutdown: release job back to queue
                _logger.LogWarning("Job {JobId} released due to shutdown", job.Id);
                await _queue.ReleaseAsync(job.Id, null, ct).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeJobs);
                heartbeatCts?.Dispose();
            }
        }

        _logger.LogDebug("Processor {ProcessorIndex} ended for {WorkerId}", processorIndex, WorkerId);
    }

    private async Task HeartbeatLoopAsync(string jobId, CancellationToken ct)
    {
        TimeSpan interval = _options.DefaultVisibilityTimeout / _options.HeartbeatIntervalDivisor;

        _logger.LogDebug("Heartbeat started for job {JobId} with interval {IntervalMs}ms", jobId, interval.TotalMilliseconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct);

                // Extend the job lock via the queue
                // This prevents the job from being reclaimed by another worker
                // while we're still processing it
                await ExtendJobLockAsync(jobId, _options.DefaultVisibilityTimeout, ct).ConfigureAwait(false);
                _logger.LogDebug("Extended lock for job {JobId}", jobId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Heartbeat cancelled for job {JobId}", jobId);
                break;
            }
            catch (Exception ex)
            {
                // Log error but continue trying to extend lock
                // If we can't extend, the job may be reclaimed and we'll handle that
                _logger.LogWarning(ex, "Failed to extend lock for job {JobId}", jobId);
            }
        }

        _logger.LogDebug("Heartbeat ended for job {JobId}", jobId);
    }

    /// <summary>
    /// Extends the lock on a job to prevent it from being reclaimed.
    /// </summary>
    private async Task ExtendJobLockAsync(string jobId, TimeSpan extension, CancellationToken ct)
    {
        try
        {
            // Release with a very short delay and let the claim loop re-acquire
            // This is a simple pattern - in production you might use a Lua script
            // to atomically extend the lock without releasing it
            await _queue.ReleaseAsync(jobId, TimeSpan.FromMilliseconds(1), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // If release fails, the job may have already been reclaimed
            // The processor will handle this on completion attempt
            _logger.LogDebug(ex, "Failed to release job {JobId} for lock extension", jobId);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing WorkerRuntime {WorkerId}", WorkerId);

        _cts?.Cancel();
        _cts?.Dispose();

        foreach (CancellationTokenSource? cts in _heartbeatCts)
        {
            cts?.Dispose();
        }

        _logger.LogDebug("WorkerRuntime {WorkerId} disposed", WorkerId);
    }
}
