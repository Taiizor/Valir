using StackExchange.Redis;
using Testcontainers.Redis;
using Valir.Abstractions;
using Valir.Core;
using Valir.Redis;

namespace Valir.Tests;

/// <summary>
/// Integration tests for RedisRecurringJobQueue using Testcontainers.
/// Tests the recurring job scheduling, claiming, and management functionality.
/// </summary>
public class RedisRecurringJobQueueIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _redis = null!;
    private RedisRecurringJobQueue _queue = null!;

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await _redisContainer.StartAsync();
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
        ValirOptions options = new()
        {
            RedisConnectionString = _redisContainer.GetConnectionString(),
            DefaultVisibilityTimeout = TimeSpan.FromSeconds(30)
        };
        _queue = new RedisRecurringJobQueue(_redis, options);
        await _queue.InitializeAsync();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _redis.Dispose();
        await _redisContainer.DisposeAsync();
    }

    #region ScheduleAsync Tests (TEST-RJ-001, TEST-RJ-002, TEST-RJ-003, TEST-RJ-004)

    [Fact]
    public async Task ScheduleAsync_ValidCronExpression_ShouldScheduleJob()
    {
        // Arrange
        string jobId = "test-job-1";
        string cronExpression = "*/5 * * * *";
        string jobType = "TestJob";
        byte[] payload = "test-payload"u8.ToArray();

        // Act
        await _queue.ScheduleAsync(jobId, cronExpression, jobType, payload, ct: TestContext.Current.CancellationToken);

        // Assert
        RecurringJobInfo? job = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(job);
        Assert.Equal(jobId, job.JobId);
        Assert.Equal(cronExpression, job.CronExpression);
        Assert.Equal(jobType, job.JobType);
        Assert.True(job.NextExecution.HasValue);
        Assert.True(job.Enabled);
    }

    [Theory]
    [InlineData("invalid-cron")]
    [InlineData("* * *")]
    [InlineData("@invalid")]
    [InlineData("")]
    public async Task ScheduleAsync_InvalidCronExpression_ShouldThrowException(string invalidCron)
    {
        // Arrange
        string jobId = "test-invalid-cron";
        string jobType = "TestJob";
        byte[] payload = "test-payload"u8.ToArray();

        // Act & Assert
        if (string.IsNullOrWhiteSpace(invalidCron))
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _queue.ScheduleAsync(jobId, invalidCron, jobType, payload, ct: TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAsync<Cronos.CronFormatException>(() =>
                _queue.ScheduleAsync(jobId, invalidCron, jobType, payload, ct: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task ScheduleAsync_ExistingJob_ShouldUpdateJob()
    {
        // Arrange
        string jobId = "test-update-job";
        string initialCron = "*/5 * * * *";
        string updatedCron = "0 */6 * * *";
        string jobType = "TestJob";
        byte[] payload = "test-payload"u8.ToArray();

        // Act - Schedule initial job
        await _queue.ScheduleAsync(jobId, initialCron, jobType, payload, ct: TestContext.Current.CancellationToken);
        RecurringJobInfo? initialJob = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        DateTimeOffset initialCreatedAt = initialJob!.CreatedAt;

        // Wait a moment to ensure UpdatedAt will be different
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Update the job
        await _queue.ScheduleAsync(jobId, updatedCron, jobType, payload, ct: TestContext.Current.CancellationToken);
        RecurringJobInfo? updatedJob = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(updatedJob);
        Assert.Equal(updatedCron, updatedJob.CronExpression);
        Assert.Equal(initialCreatedAt, updatedJob.CreatedAt); // CreatedAt should not change
        Assert.True(updatedJob.UpdatedAt > initialJob.UpdatedAt, "UpdatedAt should be newer");
    }

    [Theory]
    [InlineData(null, "* * * * *", "TestJob", new byte[] { 1 })]
    [InlineData("", "* * * * *", "TestJob", new byte[] { 1 })]
    [InlineData("job-id", null, "TestJob", new byte[] { 1 })]
    [InlineData("job-id", "", "TestJob", new byte[] { 1 })]
    [InlineData("job-id", "* * * * *", null, new byte[] { 1 })]
    [InlineData("job-id", "* * * * *", "", new byte[] { 1 })]
    [InlineData("job-id", "* * * * *", "TestJob", null)]
    public async Task ScheduleAsync_InvalidParameters_ShouldThrowArgumentException(
        string? jobId, string? cronExpression, string? jobType, byte[]? payload)
    {
        // Act & Assert
        if (jobId is null || cronExpression is null || jobType is null || payload is null)
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _queue.ScheduleAsync(
                    jobId!,
                    cronExpression!,
                    jobType!,
                    payload!,
                    ct: TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _queue.ScheduleAsync(
                    jobId,
                    cronExpression,
                    jobType,
                    payload,
                    ct: TestContext.Current.CancellationToken));
        }
    }

    #endregion

    #region ClaimDueJobsAsync Tests (TEST-RJ-005, TEST-RJ-006, TEST-RJ-007)

    [Fact]
    public async Task ClaimDueJobsAsync_DueJobsExist_ShouldClaimJobs()
    {
        // Arrange - Create a job scheduled in the past
        string jobId = "test-due-job";
        string cronExpression = "0 * * * *"; // Every hour
        string jobType = "TestJob";
        byte[] payload = "test-payload"u8.ToArray();

        // Schedule job with past execution time by using a custom options
        RecurringJobOptions options = new()
        {
            TimeZone = TimeZoneInfo.Utc
        };

        await _queue.ScheduleAsync(jobId, cronExpression, jobType, payload, options, TestContext.Current.CancellationToken);

        // Manually update the next execution to be in the past (simulate overdue job)
        DateTimeOffset pastExecution = DateTimeOffset.UtcNow.AddMinutes(-5);
        await _queue.UpdateNextExecutionAsync(jobId, pastExecution, TestContext.Current.CancellationToken);

        // Act
        RecurringJobClaimResult[] claimedJobs = await _queue.ClaimDueJobsAsync(
            "worker-1",
            batchSize: 10,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(claimedJobs);
        Assert.Equal(jobId, claimedJobs[0].JobId);
        Assert.Equal(jobType, claimedJobs[0].JobType);
    }

    [Fact]
    public async Task ClaimDueJobsAsync_JobAlreadyClaimed_ShouldNotClaimAgain()
    {
        // Arrange
        string jobId = "test-claimed-job";
        string cronExpression = "0 * * * *";
        string jobType = "TestJob";
        byte[] payload = "test-payload"u8.ToArray();

        await _queue.ScheduleAsync(jobId, cronExpression, jobType, payload, ct: TestContext.Current.CancellationToken);

        // Set past execution time
        DateTimeOffset pastExecution = DateTimeOffset.UtcNow.AddMinutes(-5);
        await _queue.UpdateNextExecutionAsync(jobId, pastExecution, TestContext.Current.CancellationToken);

        // First claim
        RecurringJobClaimResult[] firstClaim = await _queue.ClaimDueJobsAsync(
            "worker-1",
            batchSize: 10,
            ct: TestContext.Current.CancellationToken);
        Assert.Single(firstClaim);

        // Act - Try to claim again with different worker
        RecurringJobClaimResult[] secondClaim = await _queue.ClaimDueJobsAsync(
            "worker-2",
            batchSize: 10,
            ct: TestContext.Current.CancellationToken);

        // Assert - Should not claim the same job again (it's locked)
        Assert.Empty(secondClaim);
    }

    [Fact]
    public async Task ClaimDueJobsAsync_BatchSizeLimit_ShouldRespectLimit()
    {
        // Arrange - Create multiple jobs
        const int totalJobs = 5;
        const int batchSize = 2;

        for (int i = 0; i < totalJobs; i++)
        {
            string jobId = $"test-batch-job-{i}";
            await _queue.ScheduleAsync(
                jobId,
                "0 * * * *",
                "TestJob",
                System.Text.Encoding.UTF8.GetBytes($"payload-{i}"),
                ct: TestContext.Current.CancellationToken);

            // Set past execution time
            await _queue.UpdateNextExecutionAsync(
                jobId,
                DateTimeOffset.UtcNow.AddMinutes(-i - 1),
                TestContext.Current.CancellationToken);
        }

        // Act
        RecurringJobClaimResult[] claimedJobs = await _queue.ClaimDueJobsAsync(
            "worker-1",
            batchSize: batchSize,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(batchSize, claimedJobs.Length);
    }

    [Fact]
    public async Task ClaimDueJobsAsync_RaceCondition_OnlyOneWorkerClaimsJob()
    {
        // Arrange
        string jobId = "test-race-job";
        await _queue.ScheduleAsync(
            jobId,
            "0 * * * *",
            "TestJob",
            "payload"u8.ToArray(),
            ct: TestContext.Current.CancellationToken);

        // Set past execution time
        await _queue.UpdateNextExecutionAsync(
            jobId,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            TestContext.Current.CancellationToken);

        // Act - Simulate 10 workers claiming simultaneously
        List<Task<RecurringJobClaimResult[]>> tasks = [];
        for (int i = 0; i < 10; i++)
        {
            string workerId = $"worker-{i}";
            tasks.Add(_queue.ClaimDueJobsAsync(workerId, batchSize: 10, ct: TestContext.Current.CancellationToken));
        }

        RecurringJobClaimResult[][] results = await Task.WhenAll(tasks);

        // Assert - Only one worker should have claimed the job
        int successfulClaims = results.Count(r => r.Length > 0);
        Assert.Equal(1, successfulClaims);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_ExistingJob_ShouldRemoveJob()
    {
        // Arrange
        string jobId = "test-remove-job";
        await _queue.ScheduleAsync(
            jobId,
            "* * * * *",
            "TestJob",
            "payload"u8.ToArray(),
            ct: TestContext.Current.CancellationToken);

        // Verify job exists
        RecurringJobInfo? jobBefore = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(jobBefore);

        // Act
        await _queue.RemoveAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        RecurringJobInfo? jobAfter = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        Assert.Null(jobAfter);
    }

    [Fact]
    public async Task RemoveAsync_NonExistentJob_ShouldNotThrow()
    {
        // Act & Assert - Should not throw
        await _queue.RemoveAsync("non-existent-job", TestContext.Current.CancellationToken);
    }

    #endregion

    #region EnableAsync/DisableAsync Tests

    [Fact]
    public async Task DisableAsync_ExistingJob_ShouldDisableJob()
    {
        // Arrange
        string jobId = "test-disable-job";
        await _queue.ScheduleAsync(
            jobId,
            "* * * * *",
            "TestJob",
            "payload"u8.ToArray(),
            ct: TestContext.Current.CancellationToken);

        // Act
        await _queue.DisableAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        RecurringJobInfo? job = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(job);
        Assert.False(job.Enabled);
    }

    [Fact]
    public async Task EnableAsync_DisabledJob_ShouldEnableJob()
    {
        // Arrange
        string jobId = "test-enable-job";
        await _queue.ScheduleAsync(
            jobId,
            "* * * * *",
            "TestJob",
            "payload"u8.ToArray(),
            ct: TestContext.Current.CancellationToken);
        await _queue.DisableAsync(jobId, TestContext.Current.CancellationToken);

        // Verify disabled
        RecurringJobInfo? disabledJob = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        Assert.False(disabledJob!.Enabled);

        // Act
        await _queue.EnableAsync(jobId, TestContext.Current.CancellationToken);

        // Assert
        RecurringJobInfo? enabledJob = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(enabledJob);
        Assert.True(enabledJob.Enabled);
        Assert.True(enabledJob.NextExecution.HasValue);
    }

    [Fact]
    public async Task DisableAsync_NonExistentJob_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _queue.DisableAsync("non-existent-job", TestContext.Current.CancellationToken));
    }

    #endregion

    #region MisfirePolicy Tests (TEST-RJ-008, TEST-RJ-009, TEST-RJ-010, TEST-RJ-011)

    [Theory]
    [InlineData(MisfirePolicy.Skip)]
    [InlineData(MisfirePolicy.FireOnce)]
    [InlineData(MisfirePolicy.FireAll)]
    [InlineData(MisfirePolicy.FireNow)]
    public async Task ScheduleAsync_WithMisfirePolicy_ShouldStorePolicy(MisfirePolicy policy)
    {
        // Arrange
        string jobId = $"test-misfire-{policy}";
        RecurringJobOptions options = new()
        {
            MisfirePolicy = policy
        };

        // Act
        await _queue.ScheduleAsync(
            jobId,
            "* * * * *",
            "TestJob",
            "payload"u8.ToArray(),
            options,
            TestContext.Current.CancellationToken);

        // Assert
        RecurringJobInfo? job = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(job);
        Assert.Equal(policy, job.MisfirePolicy);
    }

    [Fact]
    public async Task ClaimDueJobsAsync_DisabledJob_ShouldNotBeClaimed()
    {
        // Arrange
        string jobId = "test-disabled-claim";
        await _queue.ScheduleAsync(
            jobId,
            "* * * * *",
            "TestJob",
            "payload"u8.ToArray(),
            ct: TestContext.Current.CancellationToken);

        // Set past execution time
        await _queue.UpdateNextExecutionAsync(
            jobId,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            TestContext.Current.CancellationToken);

        // Disable the job
        await _queue.DisableAsync(jobId, TestContext.Current.CancellationToken);

        // Act
        RecurringJobClaimResult[] claimedJobs = await _queue.ClaimDueJobsAsync(
            "worker-1",
            batchSize: 10,
            ct: TestContext.Current.CancellationToken);

        // Assert - Disabled job should not be claimed
        Assert.Empty(claimedJobs);
    }

    #endregion

    #region TimeZone Tests (TEST-RJ-012)

    [Theory]
    [InlineData("UTC")]
    [InlineData("Eastern Standard Time")]
    [InlineData("Central European Standard Time")]
    public async Task ScheduleAsync_WithTimeZone_ShouldRespectTimeZone(string timeZoneId)
    {
        // Arrange
        string jobId = $"test-tz-{timeZoneId.Replace(" ", "-")}";
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        RecurringJobOptions options = new()
        {
            TimeZone = timeZone
        };

        // Act
        await _queue.ScheduleAsync(
            jobId,
            "0 12 * * *", // At 12:00 PM
            "TestJob",
            "payload"u8.ToArray(),
            options,
            TestContext.Current.CancellationToken);

        // Assert
        RecurringJobInfo? job = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(job);
        Assert.NotNull(job.TimeZone);
        Assert.Equal(timeZoneId, job.TimeZone.Id);
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_MultipleJobs_ShouldReturnAllJobs()
    {
        // Arrange
        const int jobCount = 3;
        for (int i = 0; i < jobCount; i++)
        {
            await _queue.ScheduleAsync(
                $"test-all-job-{i}",
                "* * * * *",
                "TestJob",
                System.Text.Encoding.UTF8.GetBytes($"payload-{i}"),
                ct: TestContext.Current.CancellationToken);
        }

        // Act
        RecurringJobInfo[] allJobs = await _queue.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(jobCount, allJobs.Length);
    }

    [Fact]
    public async Task GetAllAsync_NoJobs_ShouldReturnEmptyArray()
    {
        // Act
        RecurringJobInfo[] allJobs = await _queue.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(allJobs);
    }

    #endregion

    #region CronFormat Tests

    [Fact]
    public async Task ScheduleAsync_WithSecondsCronFormat_ShouldAcceptSixFieldExpression()
    {
        // Arrange
        string jobId = "test-seconds-cron";
        RecurringJobOptions options = new()
        {
            CronFormat = CronFormat.IncludeSeconds
        };

        // Act - 6-field cron expression (with seconds)
        await _queue.ScheduleAsync(
            jobId,
            "0 */5 * * * *", // Every 5 minutes (with seconds field)
            "TestJob",
            "payload"u8.ToArray(),
            options,
            TestContext.Current.CancellationToken);

        // Assert
        RecurringJobInfo? job = await _queue.GetAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(job);
        Assert.True(job.NextExecution.HasValue);
    }

    #endregion
}
