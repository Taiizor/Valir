using StackExchange.Redis;
using Testcontainers.Redis;
using Valir.Abstractions;
using Valir.Core;
using Valir.Redis;

namespace Valir.Tests;

/// <summary>
/// Integration tests for Redis job queue using Testcontainers.
/// </summary>
public class RedisJobQueueIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _redis = null!;
    private RedisJobQueue _queue = null!;

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await _redisContainer.StartAsync();
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
        _queue = new RedisJobQueue(_redis, new ValirOptions());
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _redis.Dispose();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task EnqueueAsync_ShouldReturnJobId()
    {
        // Arrange
        byte[] payload = "test-payload"u8.ToArray();

        // Act
        string jobId = await _queue.EnqueueAsync("test-job", payload);

        // Assert
        Assert.NotNull(jobId);
        Assert.NotEmpty(jobId);
    }

    [Fact]
    public async Task ClaimAsync_ShouldReturnEnqueuedJob()
    {
        // Arrange
        byte[] payload = "claim-test"u8.ToArray();
        string jobId = await _queue.EnqueueAsync("claim-job", payload);

        // Act
        JobEnvelope? job = await _queue.ClaimAsync("worker-1", TimeSpan.FromSeconds(30));

        // Assert
        Assert.NotNull(job);
        Assert.Equal("claim-job", job.Type);
        Assert.Equal(1, job.Attempts);
    }

    [Fact]
    public async Task CompleteAsync_ShouldRemoveJob()
    {
        // Arrange
        byte[] payload = "complete-test"u8.ToArray();
        await _queue.EnqueueAsync("complete-job", payload);
        JobEnvelope? job = await _queue.ClaimAsync("worker-1", TimeSpan.FromSeconds(30));

        // Act
        await _queue.CompleteAsync(job!.Id);

        // Assert - no job should be claimable
        JobEnvelope? nextJob = await _queue.ClaimAsync("worker-2", TimeSpan.FromSeconds(30));
        Assert.Null(nextJob);
    }

    [Fact]
    public async Task EnqueueBatchAsync_ShouldEnqueueMultipleJobs()
    {
        // Arrange
        (string, byte[], string?)[] jobs = new[]
        {
            ("batch-job-1", "payload1"u8.ToArray(), (string?)null),
            ("batch-job-2", "payload2"u8.ToArray(), (string?)null),
            ("batch-job-3", "payload3"u8.ToArray(), (string?)null)
        };

        // Act
        string[] jobIds = await _queue.EnqueueBatchAsync(jobs);

        // Assert
        Assert.Equal(3, jobIds.Length);
        Assert.All(jobIds, Assert.NotEmpty);
    }

    [Fact]
    public async Task Priority_HigherPriorityJobsProcessedFirst()
    {
        // Arrange - enqueue low priority first, then high priority
        await _queue.EnqueueAsync("low-priority", "low"u8.ToArray(), priority: 0);
        await _queue.EnqueueAsync("high-priority", "high"u8.ToArray(), priority: 10);

        // Act - claim should get high priority first
        JobEnvelope? firstJob = await _queue.ClaimAsync("worker-1", TimeSpan.FromSeconds(30));

        // Assert
        Assert.NotNull(firstJob);
        Assert.Equal("high-priority", firstJob.Type);
    }
}
