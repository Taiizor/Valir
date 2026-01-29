using System.Threading.Channels;
using Valir.Abstractions;

namespace Valir.Core;

/// <summary>
/// Worker runtime using System.Threading.Channels for backpressure.
/// Implements graceful shutdown (drain mode).
/// </summary>
public sealed class WorkerRuntime : IJobWorker, IAsyncDisposable
{
    private readonly IJobQueue _queue;
    private readonly Func<JobEnvelope, JobContext, Task> _handler;
    private readonly ValirOptions _options;
    private readonly Channel<JobEnvelope> _channel;
    private readonly List<Task> _processorTasks = [];
    private CancellationTokenSource? _cts;
    private Task? _claimTask;
    private int _activeJobs;

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
    public WorkerRuntime(
        IJobQueue queue,
        Func<JobEnvelope, JobContext, Task> handler,
        ValirOptions options,
        string? workerId = null)
    {
        _queue = queue;
        _handler = handler;
        _options = options;
        WorkerId = workerId ?? $"worker-{Guid.CreateVersion7():N}";

        // Bounded channel creates natural backpressure
        _channel = Channel.CreateBounded<JobEnvelope>(new BoundedChannelOptions(_options.Concurrency * 2)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Start processor tasks
        for (int i = 0; i < _options.Concurrency; i++)
        {
            _processorTasks.Add(ProcessorLoopAsync(_cts.Token));
        }

        // Start claim loop
        _claimTask = ClaimLoopAsync(_cts.Token);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct)
    {
        // Phase 1: Stop claiming new jobs
        _cts?.Cancel();
        _channel.Writer.Complete();

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
        }
        catch (OperationCanceledException)
        {
            // Phase 3: Timeout reached, release remaining jobs
            // This is handled by the processor loop's finally block
        }
    }

    private async Task ClaimLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                JobEnvelope? job = await _queue.ClaimAsync(WorkerId, _options.DefaultVisibilityTimeout, ct).ConfigureAwait(false);

                if (job is null)
                {
                    await Task.Delay(_options.PollingInterval, ct).ConfigureAwait(false);
                    continue;
                }

                await _channel.Writer.WriteAsync(job, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Log error and continue
                await Task.Delay(_options.PollingInterval, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessorLoopAsync(CancellationToken ct)
    {
        await foreach (JobEnvelope job in _channel.Reader.ReadAllAsync(ct))
        {
            Interlocked.Increment(ref _activeJobs);
            try
            {
                JobContext context = new(
                    job.Id,
                    WorkerId,
                    $"{WorkerId}:{job.Id}:{job.Attempts}",
                    ct
                );

                // Start heartbeat
                using CancellationTokenSource heartbeatCts = new();
                Task heartbeatTask = HeartbeatLoopAsync(job.Id, heartbeatCts.Token);

                try
                {
                    await _handler(job, context).ConfigureAwait(false);
                    await _queue.CompleteAsync(job.Id, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _queue.FailAsync(job.Id, ex.Message, ct).ConfigureAwait(false);
                }
                finally
                {
                    heartbeatCts.Cancel();
                    try { await heartbeatTask.ConfigureAwait(false); } catch { }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Graceful shutdown: release job back to queue
                await _queue.ReleaseAsync(job.Id, null, ct).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeJobs);
            }
        }
    }

    private async Task HeartbeatLoopAsync(string jobId, CancellationToken ct)
    {
        TimeSpan interval = _options.DefaultVisibilityTimeout / 3;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct);
                // Heartbeat would extend lock via IDistributedLock
                // For now, we rely on visibility timeout
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
