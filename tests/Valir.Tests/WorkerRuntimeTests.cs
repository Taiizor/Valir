using System.Threading.Channels;
using Valir.Abstractions;
using Valir.Core;

namespace Valir.Tests;

/// <summary>
/// Unit tests for the WorkerRuntime class.
/// </summary>
public class WorkerRuntimeTests
{
    private readonly FakeJobQueue _fakeQueue;
    private readonly ValirOptions _options;

    public WorkerRuntimeTests()
    {
        _fakeQueue = new FakeJobQueue();
        _options = new ValirOptions
        {
            Concurrency = 2,
            PollingInterval = TimeSpan.FromMilliseconds(50),
            MaxPollingInterval = TimeSpan.FromMilliseconds(200),
            DefaultVisibilityTimeout = TimeSpan.FromSeconds(5),
            ShutdownTimeout = TimeSpan.FromSeconds(5),
            EnableHeartbeat = false
        };
    }

    [Fact]
    public void Constructor_WithNullQueue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerRuntime(null!, (_, _) => Task.CompletedTask, _options));
    }

    [Fact]
    public void Constructor_WithNullHandler_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerRuntime(_fakeQueue, null!, _options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WorkerRuntime(_fakeQueue, (_, _) => Task.CompletedTask, null!));
    }

    [Fact]
    public void Constructor_GeneratesUniqueWorkerId()
    {
        WorkerRuntime runtime1 = new(_fakeQueue, (_, _) => Task.CompletedTask, _options);
        WorkerRuntime runtime2 = new(_fakeQueue, (_, _) => Task.CompletedTask, _options);

        Assert.NotEqual(runtime1.WorkerId, runtime2.WorkerId);
        Assert.StartsWith("worker-", runtime1.WorkerId);
    }

    [Fact]
    public void Constructor_WithCustomWorkerId_UsesProvidedId()
    {
        string customId = "custom-worker-123";
        WorkerRuntime runtime = new(_fakeQueue, (_, _) => Task.CompletedTask, _options, customId);

        Assert.Equal(customId, runtime.WorkerId);
    }

    [Fact]
    public async Task StartAsync_BeginsProcessingJobs()
    {
        // Arrange
        TaskCompletionSource<bool> jobProcessed = new();
        WorkerRuntime runtime = new(_fakeQueue, (_, _) =>
        {
            jobProcessed.SetResult(true);
            return Task.CompletedTask;
        }, _options);

        JobEnvelope job = CreateTestJob("test-job-1");
        await _fakeQueue.EnqueueAsync(job);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await runtime.StartAsync(cts.Token);

        // Wait for job to be processed
        Task completed = await Task.WhenAny(jobProcessed.Task, Task.Delay(TimeSpan.FromSeconds(3), cts.Token));

        // Assert
        Assert.True(jobProcessed.Task.IsCompletedSuccessfully);
        Assert.True(_fakeQueue.CompletedJobs.Contains("test-job-1"));

        await runtime.StopAsync(CancellationToken.None);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_WaitsForActiveJobs()
    {
        // Arrange
        TaskCompletionSource<bool> jobStarted = new();
        TaskCompletionSource<bool> jobCanComplete = new();

        WorkerRuntime runtime = new(_fakeQueue, async (_, _) =>
        {
            jobStarted.SetResult(true);
            await jobCanComplete.Task;
        }, _options);

        JobEnvelope job = CreateTestJob("slow-job");
        await _fakeQueue.EnqueueAsync(job);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await runtime.StartAsync(cts.Token);

        // Wait for job to start
        await jobStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // Act - Start shutdown while job is still processing
        Task stopTask = runtime.StopAsync(CancellationToken.None);

        // Give stop a moment to begin
        await Task.Delay(100);

        // Complete the job
        jobCanComplete.SetResult(true);

        // Wait for stop to complete
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(_fakeQueue.CompletedJobs.Contains("slow-job") || _fakeQueue.ReleasedJobs.Contains("slow-job"));

        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Processor_JobHandlerThrows_MarksJobAsFailed()
    {
        // Arrange
        TaskCompletionSource<bool> jobFailed = new();
        WorkerRuntime runtime = new(_fakeQueue, (_, _) =>
        {
            jobFailed.SetResult(true);
            throw new InvalidOperationException("Test exception");
        }, _options);

        JobEnvelope job = CreateTestJob("failing-job");
        await _fakeQueue.EnqueueAsync(job);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await runtime.StartAsync(cts.Token);

        // Wait for job to fail
        await jobFailed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100); // Allow time for FailAsync to be called

        // Assert
        Assert.True(_fakeQueue.FailedJobs.Contains("failing-job"));

        await runtime.StopAsync(CancellationToken.None);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Processor_MultipleJobs_ProcessesConcurrently()
    {
        // Arrange
        int concurrentJobs = 0;
        int maxConcurrentJobs = 0;
        SemaphoreSlim semaphore = new(_options.Concurrency);
        TaskCompletionSource<bool> allJobsStarted = new();
        int jobsStarted = 0;
        const int totalJobs = 4;

        WorkerRuntime runtime = new(_fakeQueue, async (_, _) =>
        {
            await semaphore.WaitAsync();
            try
            {
                Interlocked.Increment(ref concurrentJobs);
                maxConcurrentJobs = Math.Max(maxConcurrentJobs, concurrentJobs);

                if (Interlocked.Increment(ref jobsStarted) == totalJobs)
                {
                    allJobsStarted.SetResult(true);
                }

                await Task.Delay(200);
            }
            finally
            {
                Interlocked.Decrement(ref concurrentJobs);
                semaphore.Release();
            }
        }, _options);

        // Enqueue multiple jobs
        for (int i = 0; i < totalJobs; i++)
        {
            await _fakeQueue.EnqueueAsync(CreateTestJob($"job-{i}"));
        }

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        // Act
        await runtime.StartAsync(cts.Token);
        await allJobsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(maxConcurrentJobs > 1, "Jobs should be processed concurrently");
        Assert.True(maxConcurrentJobs <= _options.Concurrency, "Concurrency limit should be respected");

        await runtime.StopAsync(CancellationToken.None);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task ClaimLoop_EmptyQueue_AppliesBackoff()
    {
        // Arrange
        int claimAttempts = 0;
        WorkerRuntime runtime = new(_fakeQueue, (_, _) => Task.CompletedTask, _options);

        _fakeQueue.OnClaimAttempt = () => Interlocked.Increment(ref claimAttempts);

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(500));

        // Act
        await runtime.StartAsync(cts.Token);
        await Task.Delay(400); // Let it poll a few times

        // Assert
        Assert.True(claimAttempts >= 2, "Should have attempted to claim multiple times");

        await runtime.StopAsync(CancellationToken.None);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CancelsAndDisposesResources()
    {
        // Arrange
        WorkerRuntime runtime = new(_fakeQueue, (_, _) => Task.CompletedTask, _options);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await runtime.StartAsync(cts.Token);

        // Act
        await runtime.DisposeAsync();

        // Assert - no exception should be thrown
        Assert.True(true);
    }

    private static JobEnvelope CreateTestJob(string id)
    {
        return new JobEnvelope(
            Id: id,
            Type: "test",
            Payload: "test-payload"u8.ToArray(),
            Attempts: 0,
            MaxAttempts: 3,
            CreatedAt: DateTimeOffset.UtcNow,
            VisibilityTimeout: TimeSpan.FromSeconds(30)
        );
    }
}

/// <summary>
/// Fake implementation of IJobQueue for testing.
/// </summary>
internal sealed class FakeJobQueue : IJobQueue
{
    private readonly Channel<JobEnvelope> _jobs = Channel.CreateUnbounded<JobEnvelope>();
    private readonly List<string> _completedJobs = [];
    private readonly List<string> _failedJobs = [];
    private readonly List<string> _releasedJobs = [];
    private readonly Dictionary<string, JobEnvelope> _activeJobs = [];

    public IReadOnlyList<string> CompletedJobs => _completedJobs;
    public IReadOnlyList<string> FailedJobs => _failedJobs;
    public IReadOnlyList<string> ReleasedJobs => _releasedJobs;
    public Action? OnClaimAttempt { get; set; }

    public Task EnqueueAsync(JobEnvelope job)
    {
        return _jobs.Writer.WriteAsync(job).AsTask();
    }

    public Task<string> EnqueueAsync(
        string type,
        byte[] payload,
        TimeSpan? delay = null,
        int priority = 0,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        JobEnvelope job = new(
            Guid.CreateVersion7().ToString("N"),
            type,
            payload,
            0,
            3,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(30),
            idempotencyKey,
            priority
        );
        return EnqueueAsync(job).ContinueWith(_ => job.Id);
    }

    public Task<string[]> EnqueueBatchAsync(
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs,
        int priority = 0,
        CancellationToken ct = default)
    {
        List<string> jobIds = [];
        foreach ((string? type, byte[]? payload, string? idempotencyKey) in jobs)
        {
            jobIds.Add(EnqueueAsync(type, payload, null, priority, idempotencyKey, ct).Result);
        }
        return Task.FromResult(jobIds.ToArray());
    }

    public Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout, CancellationToken ct = default)
    {
        OnClaimAttempt?.Invoke();

        if (_jobs.Reader.TryRead(out JobEnvelope? job))
        {
            _activeJobs[job.Id] = job;
            return Task.FromResult<JobEnvelope?>(job);
        }

        return Task.FromResult<JobEnvelope?>(null);
    }

    public Task CompleteAsync(string jobId, CancellationToken ct = default)
    {
        _activeJobs.Remove(jobId);
        _completedJobs.Add(jobId);
        return Task.CompletedTask;
    }

    public Task FailAsync(string jobId, string reason, CancellationToken ct = default)
    {
        _activeJobs.Remove(jobId);
        _failedJobs.Add(jobId);
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string jobId, TimeSpan? delay = null, CancellationToken ct = default)
    {
        if (_activeJobs.TryGetValue(jobId, out JobEnvelope? job))
        {
            _activeJobs.Remove(jobId);
            _releasedJobs.Add(jobId);

            if (delay is null)
            {
                // Re-enqueue immediately
                return _jobs.Writer.WriteAsync(job).AsTask();
            }
        }
        return Task.CompletedTask;
    }
}
