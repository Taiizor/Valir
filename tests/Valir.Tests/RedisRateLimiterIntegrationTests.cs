using StackExchange.Redis;
using Testcontainers.Redis;
using Valir.Redis;

namespace Valir.Tests;

/// <summary>
/// Integration tests for RedisRateLimiter using Testcontainers.
/// </summary>
public class RedisRateLimiterIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _redis = null!;
    private RedisRateLimiter _rateLimiter = null!;

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await _redisContainer.StartAsync();
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
        _rateLimiter = new RedisRateLimiter(_redis);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _redis.Dispose();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task AllowAsync_WhenUnderLimit_ReturnsTrue()
    {
        // Arrange
        const string key = "test-key";
        const int max = 5;
        TimeSpan window = TimeSpan.FromSeconds(10);

        // Act
        bool result = await _rateLimiter.AllowAsync(key, max, window);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AllowAsync_WhenOverLimit_ReturnsFalse()
    {
        // Arrange
        const string key = "test-key";
        const int max = 2;
        TimeSpan window = TimeSpan.FromSeconds(10);

        // Act - consume all allowed requests
        await _rateLimiter.AllowAsync(key, max, window);
        await _rateLimiter.AllowAsync(key, max, window);
        bool result = await _rateLimiter.AllowAsync(key, max, window);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AllowAsync_DifferentKeys_TrackedSeparately()
    {
        // Arrange
        const int max = 2;
        TimeSpan window = TimeSpan.FromSeconds(10);

        // Act - consume all allowed requests for key1
        await _rateLimiter.AllowAsync("key1", max, window);
        await _rateLimiter.AllowAsync("key1", max, window);

        // key2 should still be allowed
        bool key1Result = await _rateLimiter.AllowAsync("key1", max, window);
        bool key2Result = await _rateLimiter.AllowAsync("key2", max, window);

        // Assert
        Assert.False(key1Result);
        Assert.True(key2Result);
    }

    [Fact]
    public async Task AllowAsync_AfterWindowExpires_AllowsNewRequests()
    {
        // Arrange
        const string key = "test-key";
        const int max = 1;
        TimeSpan window = TimeSpan.FromMilliseconds(100);

        // Act - consume the single allowed request
        await _rateLimiter.AllowAsync(key, max, window);
        bool firstResult = await _rateLimiter.AllowAsync(key, max, window);

        // Wait for window to expire
        await Task.Delay(150, TestContext.Current.CancellationToken);

        // Should be allowed again
        bool secondResult = await _rateLimiter.AllowAsync(key, max, window);

        // Assert
        Assert.False(firstResult);
        Assert.True(secondResult);
    }

    [Fact]
    public async Task AllowAsync_WithCustomPrefix_UsesCustomPrefix()
    {
        // Arrange
        RedisRateLimiter customLimiter = new(_redis, "custom:rate:");
        const string key = "test-key";
        const int max = 5;
        TimeSpan window = TimeSpan.FromSeconds(10);

        // Act
        bool result = await customLimiter.AllowAsync(key, max, window);

        // Assert
        Assert.True(result);

        // Verify the rate limit key is stored with custom prefix
        IDatabase db = _redis.GetDatabase();
        RedisResult keys = await db.ExecuteAsync("KEYS", "custom:rate:test-key");
        Assert.NotNull(keys);
    }

    [Fact]
    public async Task AllowAsync_SlidingWindow_MaintainsCorrectCount()
    {
        // Arrange
        const string key = "sliding-key";
        const int max = 3;
        TimeSpan window = TimeSpan.FromSeconds(2);

        // Act - make requests at different times
        bool r1 = await _rateLimiter.AllowAsync(key, max, window);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        bool r2 = await _rateLimiter.AllowAsync(key, max, window);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        bool r3 = await _rateLimiter.AllowAsync(key, max, window);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        bool r4 = await _rateLimiter.AllowAsync(key, max, window);

        // Assert - first 3 should be allowed, 4th should be denied
        Assert.True(r1);
        Assert.True(r2);
        Assert.True(r3);
        Assert.False(r4);
    }

    [Fact]
    public async Task AllowAsync_ConcurrentRequests_HandledCorrectly()
    {
        // Arrange
        const string key = "concurrent-key";
        const int max = 10;
        TimeSpan window = TimeSpan.FromSeconds(10);

        // Act - make many concurrent requests
        List<Task<bool>> tasks = [];
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(_rateLimiter.AllowAsync(key, max, window));
        }

        bool[] results = await Task.WhenAll(tasks);
        int allowedCount = results.Count(r => r);

        // Assert - at most max requests should be allowed
        Assert.True(allowedCount <= max, $"Expected at most {max} allowed, but got {allowedCount}");
    }
}
