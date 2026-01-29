using Moq;
using StackExchange.Redis;
using Valir.Redis;

namespace Valir.Tests;

/// <summary>
/// Unit tests for the RedisRateLimiter class.
/// </summary>
public class RedisRateLimiterTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private const string TestKey = "test-key";
    private const string KeyPrefix = "valir:rate:";

    public RedisRateLimiterTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_mockDatabase.Object);
    }

    [Fact]
    public async Task AllowAsync_WithinRateLimit_ReturnsTrue()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == KeyPrefix + TestKey),
                It.Is<RedisValue[]>(values => values.Length == 4),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act
        bool result = await rateLimiter.AllowAsync(TestKey, max: 10, window: TimeSpan.FromSeconds(60));

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(d => d.ScriptEvaluateAsync(
            It.Is<string>(s => s.Contains("ZREMRANGEBYSCORE")),
            It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == KeyPrefix + TestKey),
            It.Is<RedisValue[]>(values => values.Length == 4),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task AllowAsync_ExceedingRateLimit_ReturnsFalse()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == KeyPrefix + TestKey),
                It.Is<RedisValue[]>(values => values.Length == 4),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act
        bool result = await rateLimiter.AllowAsync(TestKey, max: 5, window: TimeSpan.FromSeconds(60));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AllowAsync_MultipleRequestsWithinLimit_AllowsUpToMax()
    {
        // Arrange
        int callCount = 0;
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return RedisResult.Create(callCount <= 3 ? 1L : 0L);
            });

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act - Make 5 requests with max=3
        bool[] results = await Task.WhenAll(
            rateLimiter.AllowAsync(TestKey, max: 3, window: TimeSpan.FromSeconds(60)),
            rateLimiter.AllowAsync(TestKey, max: 3, window: TimeSpan.FromSeconds(60)),
            rateLimiter.AllowAsync(TestKey, max: 3, window: TimeSpan.FromSeconds(60)),
            rateLimiter.AllowAsync(TestKey, max: 3, window: TimeSpan.FromSeconds(60)),
            rateLimiter.AllowAsync(TestKey, max: 3, window: TimeSpan.FromSeconds(60))
        );

        // Assert
        Assert.Equal(3, results.Count(r => r));
        Assert.Equal(2, results.Count(r => !r));
    }

    [Fact]
    public async Task AllowAsync_DifferentKeys_AreIndependent()
    {
        // Arrange
        const string key1 = "user-1";
        const string key2 = "user-2";

        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys[0] == KeyPrefix + key1),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys[0] == KeyPrefix + key2),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act
        bool result1 = await rateLimiter.AllowAsync(key1, max: 1, window: TimeSpan.FromSeconds(60));
        bool result2 = await rateLimiter.AllowAsync(key2, max: 1, window: TimeSpan.FromSeconds(60));

        // Assert - Both should be allowed because they use different keys
        Assert.True(result1);
        Assert.True(result2);
    }

    [Fact]
    public async Task AllowAsync_UsesCorrectKeyPrefix()
    {
        // Arrange
        const string customPrefix = "custom:rate:";
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == customPrefix + TestKey),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object, customPrefix);

        // Act
        bool result = await rateLimiter.AllowAsync(TestKey, max: 10, window: TimeSpan.FromSeconds(60));

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys[0] == customPrefix + TestKey),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task AllowAsync_PassesCorrectParametersToScript()
    {
        // Arrange
        long capturedNow = 0;
        long capturedWindowStart = 0;
        int capturedMax = 0;
        long capturedWindowMs = 0;

        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, _, values, _) =>
            {
                capturedNow = (long)values[0];
                capturedWindowStart = (long)values[1];
                capturedMax = (int)values[2];
                capturedWindowMs = (long)values[3];
            })
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);
        TimeSpan window = TimeSpan.FromSeconds(60);

        // Act
        await rateLimiter.AllowAsync(TestKey, max: 10, window: window);

        // Assert
        Assert.True(capturedNow > 0);
        Assert.Equal(60 * 1000, capturedWindowMs);
        Assert.Equal(10, capturedMax);
        Assert.True(capturedWindowStart < capturedNow);
        Assert.Equal(capturedNow - capturedWindowMs, capturedWindowStart);
    }

    [Fact]
    public async Task AllowAsync_SlidingWindow_RemovesExpiredEntries()
    {
        // Arrange
        string? capturedScript = null;
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, _, _, _) =>
            {
                capturedScript = script;
            })
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act
        await rateLimiter.AllowAsync(TestKey, max: 10, window: TimeSpan.FromSeconds(60));

        // Assert
        Assert.NotNull(capturedScript);
        Assert.Contains("ZREMRANGEBYSCORE", capturedScript);
        Assert.Contains("-inf", capturedScript);
        Assert.Contains("windowStart", capturedScript);
    }

    [Fact]
    public async Task AllowAsync_AfterWindowExpires_AllowsNewRequests()
    {
        // Arrange - First call returns 0 (rate limited), second call returns 1 (allowed after window)
        int callCount = 0;
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call rate limited, subsequent calls allowed (simulating window expiry)
                return RedisResult.Create(callCount == 1 ? 0L : 1L);
            });

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act
        bool firstResult = await rateLimiter.AllowAsync(TestKey, max: 1, window: TimeSpan.FromSeconds(1));
        bool secondResult = await rateLimiter.AllowAsync(TestKey, max: 1, window: TimeSpan.FromSeconds(1));

        // Assert
        Assert.False(firstResult);
        Assert.True(secondResult);
    }

    [Fact]
    public async Task AllowAsync_WithVeryHighRateLimit_AllowsManyRequests()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act - Make 100 requests with max=1000
        List<Task<bool>> tasks = [];
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(rateLimiter.AllowAsync(TestKey, max: 1000, window: TimeSpan.FromSeconds(60)));
        }

        bool[] results = await Task.WhenAll(tasks);

        // Assert - All should be allowed
        Assert.All(results, Assert.True);
    }

    [Fact]
    public async Task AllowAsync_ConcurrentRequests_HandlesRaceConditions()
    {
        // Arrange
        int allowedCount = 0;
        SemaphoreSlim semaphore = new(1, 1);

        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                semaphore.Wait();
                try
                {
                    // Simulate atomic check-and-set
                    if (allowedCount < 5)
                    {
                        allowedCount++;
                        return RedisResult.Create(1L);
                    }
                    return RedisResult.Create(0L);
                }
                finally
                {
                    semaphore.Release();
                }
            });

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act - Simulate 10 concurrent requests with max=5
        List<Task<bool>> tasks = [];
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(rateLimiter.AllowAsync(TestKey, max: 5, window: TimeSpan.FromSeconds(60)));
        }

        bool[] results = await Task.WhenAll(tasks);

        // Assert - Exactly 5 should be allowed
        Assert.Equal(5, results.Count(r => r));
        Assert.Equal(5, results.Count(r => !r));
    }

    [Fact]
    public async Task AllowAsync_SetsExpirationOnKey()
    {
        // Arrange
        string? capturedScript = null;
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, _, _, _) =>
            {
                capturedScript = script;
            })
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act
        await rateLimiter.AllowAsync(TestKey, max: 10, window: TimeSpan.FromSeconds(60));

        // Assert
        Assert.NotNull(capturedScript);
        Assert.Contains("PEXPIRE", capturedScript);
        Assert.Contains("windowMs", capturedScript);
    }

    [Fact]
    public async Task AllowAsync_AddsEntryWithTimestampAndRandomSuffix()
    {
        // Arrange
        string? capturedScript = null;
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, _, _, _) =>
            {
                capturedScript = script;
            })
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);

        // Act
        await rateLimiter.AllowAsync(TestKey, max: 10, window: TimeSpan.FromSeconds(60));

        // Assert
        Assert.NotNull(capturedScript);
        Assert.Contains("ZADD", capturedScript);
        Assert.Contains("math.random()", capturedScript);
    }

    [Fact]
    public async Task AllowAsync_WithShortWindow_CalculatesCorrectWindowStart()
    {
        // Arrange
        long capturedWindowStart = 0;
        long capturedNow = 0;

        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, _, values, _) =>
            {
                capturedNow = (long)values[0];
                capturedWindowStart = (long)values[1];
            })
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);
        TimeSpan shortWindow = TimeSpan.FromMilliseconds(100);

        // Act
        await rateLimiter.AllowAsync(TestKey, max: 10, window: shortWindow);

        // Assert
        Assert.Equal(100, capturedNow - capturedWindowStart);
    }

    [Fact]
    public async Task AllowAsync_WithLongWindow_CalculatesCorrectWindowStart()
    {
        // Arrange
        long capturedWindowStart = 0;
        long capturedNow = 0;

        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, _, values, _) =>
            {
                capturedNow = (long)values[0];
                capturedWindowStart = (long)values[1];
            })
            .ReturnsAsync(RedisResult.Create(1L));

        RedisRateLimiter rateLimiter = new(_mockRedis.Object);
        TimeSpan longWindow = TimeSpan.FromHours(1);

        // Act
        await rateLimiter.AllowAsync(TestKey, max: 10, window: longWindow);

        // Assert
        Assert.Equal(3600000, capturedNow - capturedWindowStart);
    }
}
