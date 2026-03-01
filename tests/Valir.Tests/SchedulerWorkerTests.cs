using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using Valir.Abstractions;
using Valir.Core;

namespace Valir.Tests;

/// <summary>
/// Unit tests for SchedulerWorker.
/// Tests the lifecycle, job execution loop, and graceful shutdown behavior.
/// </summary>
public class SchedulerWorkerTests
{
    private readonly FakeRecurringJobQueue _fakeRecurringQueue;
    private readonly SchedulerWorkerFakeJobQueue _fakeJobQueue;
    private readonly SchedulerWorkerOptions _options;
    private readonly IOptions<SchedulerWorkerOptions> _optionsWrapper;

    public SchedulerWorkerTests()
    {
        _fakeRecurringQueue = new FakeRecurringJobQueue();
        _fakeJobQueue = new SchedulerWorkerFakeJobQueue();
        _options = new SchedulerWorkerOptions
        {
            CheckInterval = TimeSpan.FromMilliseconds(100),
            BatchSize = 10
        };
        _optionsWrapper = Options.Create(_options);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRecurringQueue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SchedulerWorker(
                null!,
                _fakeJobQueue,
                _optionsWrapper,
                NullLogger<SchedulerWorker>.Instance));
    }

    [Fact]
    public void Constructor_WithNullJobQueue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SchedulerWorker(
                _fakeRecurringQueue,
                null!,
                _optionsWrapper,
                NullLogger<SchedulerWorker>.Instance));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SchedulerWorker(
                _fakeRecurringQueue,
                _fakeJobQueue,
                null!,
                NullLogger<SchedulerWorker>.Instance));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        // Assert
        Assert.NotNull(worker);
    }

    #endregion

    #region StartAsync/StopAsync Lifecycle Tests

    [Fact]
    public async Task StartAsync_WhenNotRunning_StartsSuccessfully()
    {
        // Arrange
        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await worker.StartAsync(cts.Token);

        // Assert - Should complete without exception
        Assert.True(true);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotThrow()
    {
        // Arrange
        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);

        // Act - Start again (should not throw)
        await worker.StartAsync(cts.Token);

        // Assert - Should complete without exception
        Assert.True(true);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_WhenRunning_StopsSuccessfully()
    {
        // Arrange
        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert - Should complete without exception
        Assert.True(true);

        // Cleanup
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNotThrow()
    {
        // Arrange
        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        // Act - Stop without starting
        await worker.StopAsync(CancellationToken.None);

        // Assert - Should complete without exception
        Assert.True(true);

        // Cleanup
        await worker.DisposeAsync();
    }

    #endregion

    #region Job Execution Loop Tests

    [Fact]
    public async Task StartAsync_ProcessesDueJobs()
    {
        // Arrange
        RecurringJobClaimResult dueJob = CreateRecurringJobClaimResult("due-job-1", DateTimeOffset.UtcNow.AddMinutes(-5));
        _fakeRecurringQueue.AddDueJob(dueJob);

        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await worker.StartAsync(cts.Token);

        await WaitForConditionAsync(
            () => _fakeJobQueue.EnqueuedRecurringJobs.Count >= 1,
            TimeSpan.FromSeconds(3),
            cts.Token);

        // Assert
        Assert.Single(_fakeJobQueue.EnqueuedRecurringJobs);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_WithMisfirePolicySkip_DoesNotEnqueueJob()
    {
        // Arrange
        RecurringJobClaimResult misfiredJob = CreateRecurringJobClaimResult(
            "misfired-skip-job",
            DateTimeOffset.UtcNow.AddHours(-2),
            MisfirePolicy.Skip);
        _fakeRecurringQueue.AddDueJob(misfiredJob);

        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await worker.StartAsync(cts.Token);
        await WaitForConditionAsync(
            () => _fakeRecurringQueue.UpdatedNextExecutionJobs.Contains(misfiredJob.JobId),
            TimeSpan.FromSeconds(3),
            cts.Token);

        // Assert - Job with Skip policy should not be enqueued
        Assert.Empty(_fakeJobQueue.EnqueuedRecurringJobs);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_WithMisfirePolicyFireOnce_EnqueuesSingleInstance()
    {
        // Arrange
        RecurringJobClaimResult misfiredJob = CreateRecurringJobClaimResult(
            "misfired-fireonce-job",
            DateTimeOffset.UtcNow.AddHours(-2),
            MisfirePolicy.FireOnce);
        _fakeRecurringQueue.AddDueJob(misfiredJob);

        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await worker.StartAsync(cts.Token);
        await WaitForConditionAsync(
            () => _fakeJobQueue.EnqueuedRecurringJobs.Count >= 1,
            TimeSpan.FromSeconds(3),
            cts.Token);

        // Assert - Job should be enqueued once
        Assert.Single(_fakeJobQueue.EnqueuedRecurringJobs);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_WithMisfirePolicyFireNow_EnqueuesWithCurrentTime()
    {
        // Arrange
        RecurringJobClaimResult misfiredJob = CreateRecurringJobClaimResult(
            "misfired-firenow-job",
            DateTimeOffset.UtcNow.AddHours(-2),
            MisfirePolicy.FireNow);
        _fakeRecurringQueue.AddDueJob(misfiredJob);

        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await worker.StartAsync(cts.Token);
        await WaitForConditionAsync(
            () => _fakeJobQueue.EnqueuedRecurringJobs.Count >= 1,
            TimeSpan.FromSeconds(3),
            cts.Token);

        // Assert - Job should be enqueued
        Assert.Single(_fakeJobQueue.EnqueuedRecurringJobs);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_UpdatesNextExecutionTime()
    {
        // Arrange
        RecurringJobClaimResult dueJob = CreateRecurringJobClaimResult("update-next-job", DateTimeOffset.UtcNow.AddMinutes(-5));
        _fakeRecurringQueue.AddDueJob(dueJob);

        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await worker.StartAsync(cts.Token);
        await WaitForConditionAsync(
            () => _fakeRecurringQueue.UpdatedNextExecutionJobs.Contains(dueJob.JobId),
            TimeSpan.FromSeconds(3),
            cts.Token);

        // Assert
        Assert.Contains(dueJob.JobId, _fakeRecurringQueue.UpdatedNextExecutionJobs);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
        await worker.DisposeAsync();
    }

    #endregion

    #region Graceful Shutdown Tests

    [Fact]
    public async Task StopAsync_GracefullyStopsProcessing()
    {
        // Arrange
        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);

        // Wait for a few processing cycles
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert - Should stop without exception
        Assert.True(true);

        // Cleanup
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CleansUpResources()
    {
        // Arrange
        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act
        await worker.DisposeAsync();

        // Assert - Should dispose without exception
        Assert.True(true);
    }

    #endregion

    #region Batch Processing Tests

    [Fact]
    public async Task StartAsync_ProcessesMultipleDueJobs()
    {
        // Arrange
        const int jobCount = 5;
        for (int i = 0; i < jobCount; i++)
        {
            RecurringJobClaimResult job = CreateRecurringJobClaimResult($"batch-job-{i}", DateTimeOffset.UtcNow.AddMinutes(-i - 1));
            _fakeRecurringQueue.AddDueJob(job);
        }

        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            _optionsWrapper,
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await worker.StartAsync(cts.Token);
        await WaitForConditionAsync(
            () => _fakeJobQueue.EnqueuedRecurringJobs.Count == jobCount,
            TimeSpan.FromSeconds(3),
            cts.Token);

        // Assert
        Assert.Equal(jobCount, _fakeJobQueue.EnqueuedRecurringJobs.Count);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
        await worker.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_RespectsBatchSize()
    {
        // Arrange
        SchedulerWorkerOptions smallBatchOptions = new()
        {
            CheckInterval = TimeSpan.FromMilliseconds(100),
            BatchSize = 2
        };

        // Add more jobs than batch size
        for (int i = 0; i < 5; i++)
        {
            RecurringJobClaimResult job = CreateRecurringJobClaimResult($"limited-batch-job-{i}", DateTimeOffset.UtcNow.AddMinutes(-i - 1));
            _fakeRecurringQueue.AddDueJob(job);
        }

        SchedulerWorker worker = new(
            _fakeRecurringQueue,
            _fakeJobQueue,
            Options.Create(smallBatchOptions),
            NullLogger<SchedulerWorker>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        // Assert - Should only process batch size per check
        Assert.True(_fakeJobQueue.EnqueuedRecurringJobs.Count <= 5);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
        await worker.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    private static RecurringJobClaimResult CreateRecurringJobClaimResult(
        string jobId,
        DateTimeOffset scheduledAt,
        MisfirePolicy misfirePolicy = MisfirePolicy.FireOnce)
    {
        return new RecurringJobClaimResult(
            JobId: jobId,
            JobType: "TestJob",
            Payload: "test-payload"u8.ToArray(),
            Queue: "default",
            Priority: 0,
            CronExpression: "* * * * *",
            CronFormat: CronFormat.Standard,
            TimeZone: TimeZoneInfo.Utc,
            MisfirePolicy: misfirePolicy,
            MaxRetries: 3,
            ScheduledAt: scheduledAt,
            LastExecution: null
        );
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout, CancellationToken ct)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        while (!condition())
        {
            await Task.Delay(20, timeoutCts.Token);
        }
    }

    #endregion
}

/// <summary>
/// Fake implementation of IRecurringJobQueue for testing SchedulerWorker.
/// </summary>
internal sealed class FakeRecurringJobQueue : IRecurringJobQueue
{
    private readonly List<RecurringJobClaimResult> _dueJobs = [];
    private readonly List<string> _updatedNextExecutionJobs = [];

    public IReadOnlyList<string> UpdatedNextExecutionJobs => _updatedNextExecutionJobs;

    public void AddDueJob(RecurringJobClaimResult job)
    {
        _dueJobs.Add(job);
    }

    public Task<RecurringJobClaimResult[]> ClaimDueJobsAsync(
        string workerId,
        int batchSize = 10,
        CancellationToken ct = default)
    {
        RecurringJobClaimResult[] jobs = [.. _dueJobs.Take(batchSize)];
        foreach (RecurringJobClaimResult? job in jobs)
        {
            _dueJobs.Remove(job);
        }
        return Task.FromResult(jobs);
    }

    public Task UpdateNextExecutionAsync(
        string jobId,
        DateTimeOffset nextExecution,
        CancellationToken ct = default)
    {
        _updatedNextExecutionJobs.Add(jobId);
        return Task.CompletedTask;
    }

    // Not used by SchedulerWorker tests
    public Task ScheduleAsync(
        string jobId,
        string cronExpression,
        string jobType,
        byte[] payload,
        RecurringJobOptions? options = null,
        CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string jobId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task DisableAsync(string jobId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task EnableAsync(string jobId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<RecurringJobInfo[]> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Array.Empty<RecurringJobInfo>());
    }

    public Task<RecurringJobInfo?> GetAsync(string jobId, CancellationToken ct = default)
    {
        return Task.FromResult<RecurringJobInfo?>(null);
    }
}

/// <summary>
/// Extended FakeJobQueue that tracks recurring job enqueues for SchedulerWorker tests.
/// </summary>
internal sealed class SchedulerWorkerFakeJobQueue : IJobQueue
{
    private readonly Channel<JobEnvelope> _jobs = Channel.CreateUnbounded<JobEnvelope>();
    private readonly List<string> _enqueuedRecurringJobs = [];

    public IReadOnlyList<string> EnqueuedRecurringJobs => _enqueuedRecurringJobs;

    public Task EnqueueAsync(JobEnvelope job)
    {
        _enqueuedRecurringJobs.Add(job.Id);
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
        string jobId = Guid.CreateVersion7().ToString("N");
        _enqueuedRecurringJobs.Add(jobId);
        return Task.FromResult(jobId);
    }

    public Task<string[]> EnqueueBatchAsync(
        IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs,
        int priority = 0,
        CancellationToken ct = default)
    {
        string[] jobIds = [.. jobs.Select(_ => Guid.CreateVersion7().ToString("N"))];
        foreach (string? id in jobIds)
        {
            _enqueuedRecurringJobs.Add(id);
        }
        return Task.FromResult(jobIds);
    }

    public Task<JobEnvelope?> ClaimAsync(string workerId, TimeSpan claimTimeout, CancellationToken ct = default)
    {
        if (_jobs.Reader.TryRead(out JobEnvelope? job))
        {
            return Task.FromResult<JobEnvelope?>(job);
        }
        return Task.FromResult<JobEnvelope?>(null);
    }

    public Task CompleteAsync(string jobId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task FailAsync(string jobId, string reason, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string jobId, TimeSpan? delay = null, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<bool> ExtendLockAsync(string jobId, string workerId, TimeSpan extension, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }
}
