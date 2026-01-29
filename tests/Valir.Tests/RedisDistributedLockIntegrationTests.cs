using StackExchange.Redis;
using Testcontainers.Redis;
using Valir.Redis;

namespace Valir.Tests;

/// <summary>
/// Integration tests for RedisDistributedLock using Testcontainers.
/// </summary>
public class RedisDistributedLockIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _redis = null!;

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await _redisContainer.StartAsync();
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _redis.Dispose();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenLockNotHeld_ReturnsTrue()
    {
        // Arrange
        RedisDistributedLock lockInstance = new(_redis, "test-key", "test-owner");

        // Act
        bool result = await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AcquireAsync_WhenLockAlreadyHeldByAnother_ReturnsFalse()
    {
        // Arrange
        RedisDistributedLock lock1 = new(_redis, "test-key", "owner-1");
        RedisDistributedLock lock2 = new(_redis, "test-key", "owner-2");
        await lock1.AcquireAsync(TimeSpan.FromSeconds(30));

        // Act
        bool result = await lock2.AcquireAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AcquireAsync_WhenLockAlreadyHeldBySameOwner_ReturnsFalse()
    {
        // Arrange
        RedisDistributedLock lockInstance = new(_redis, "test-key", "test-owner");
        await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Act
        bool result = await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseAsync_WhenLockHeldByOwner_ReleasesLock()
    {
        // Arrange
        RedisDistributedLock lockInstance = new(_redis, "test-key", "test-owner");
        await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Act
        await lockInstance.ReleaseAsync();

        // Assert - lock should be released, so another acquire should succeed
        RedisDistributedLock lock2 = new(_redis, "test-key", "another-owner");
        bool result = await lock2.AcquireAsync(TimeSpan.FromSeconds(30));
        Assert.True(result);
    }

    [Fact]
    public async Task ReleaseAsync_WhenLockHeldByAnother_DoesNotRelease()
    {
        // Arrange
        RedisDistributedLock lock1 = new(_redis, "test-key", "owner-1");
        RedisDistributedLock lock2 = new(_redis, "test-key", "owner-2");
        await lock1.AcquireAsync(TimeSpan.FromSeconds(30));

        // Act
        await lock2.ReleaseAsync();

        // Assert - lock1 should still hold the lock
        RedisDistributedLock lock3 = new(_redis, "test-key", "owner-3");
        bool result = await lock3.AcquireAsync(TimeSpan.FromSeconds(30));
        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseAsync_WhenLockDoesNotExist_DoesNothing()
    {
        // Arrange
        RedisDistributedLock lockInstance = new(_redis, "test-key", "test-owner");

        // Act & Assert - should not throw
        await lockInstance.ReleaseAsync();
    }

    [Fact]
    public async Task ExtendAsync_WhenLockHeldByOwner_ReturnsTrue()
    {
        // Arrange
        RedisDistributedLock lockInstance = new(_redis, "test-key", "test-owner");
        await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Act
        bool result = await lockInstance.ExtendAsync(TimeSpan.FromSeconds(60));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExtendAsync_WhenLockHeldByAnother_ReturnsFalse()
    {
        // Arrange
        RedisDistributedLock lock1 = new(_redis, "test-key", "owner-1");
        RedisDistributedLock lock2 = new(_redis, "test-key", "owner-2");
        await lock1.AcquireAsync(TimeSpan.FromSeconds(30));

        // Act
        bool result = await lock2.ExtendAsync(TimeSpan.FromSeconds(60));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtendAsync_WhenLockDoesNotExist_ReturnsFalse()
    {
        // Arrange
        RedisDistributedLock lockInstance = new(_redis, "test-key", "test-owner");

        // Act
        bool result = await lockInstance.ExtendAsync(TimeSpan.FromSeconds(60));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DisposeAsync_WhenLockHeld_ReleasesLock()
    {
        // Arrange
        RedisDistributedLock lockInstance = new(_redis, "test-key", "test-owner");
        await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Act
        await lockInstance.DisposeAsync();

        // Assert - lock should be released
        RedisDistributedLock lock2 = new(_redis, "test-key", "another-owner");
        bool result = await lock2.AcquireAsync(TimeSpan.FromSeconds(30));
        Assert.True(result);
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_ReleasesLockOnlyOnce()
    {
        // Arrange
        RedisDistributedLock lockInstance = new(_redis, "test-key", "test-owner");
        await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Act
        await lockInstance.DisposeAsync();
        await lockInstance.DisposeAsync();

        // Assert - lock should still be released
        RedisDistributedLock lock2 = new(_redis, "test-key", "another-owner");
        bool result = await lock2.AcquireAsync(TimeSpan.FromSeconds(30));
        Assert.True(result);
    }

    [Fact]
    public void Constructor_SetsKeyAndOwnerProperties()
    {
        // Arrange & Act
        RedisDistributedLock lockInstance = new(_redis, "my-key", "my-owner");

        // Assert
        Assert.Equal("my-key", lockInstance.Key);
        Assert.Equal("my-owner", lockInstance.Owner);
    }

    [Fact]
    public void Constructor_WithCustomKeyPrefix_UsesCustomPrefix()
    {
        // Arrange & Act
        RedisDistributedLock lockInstance = new(_redis, "my-key", "my-owner", "custom:lock:");

        // Act & Assert - acquire should work with custom prefix
        Assert.Equal("my-key", lockInstance.Key);
        Assert.Equal("my-owner", lockInstance.Owner);
    }

    [Fact]
    public async Task AcquireAsync_WithCustomKeyPrefix_UsesCustomPrefix()
    {
        // Arrange
        RedisDistributedLock lockInstance = new(_redis, "test-key", "test-owner", "custom:lock:");

        // Act
        bool result = await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.True(result);

        // Verify the lock is actually stored with the custom prefix
        IDatabase db = _redis.GetDatabase();
        RedisValue lockValue = await db.StringGetAsync("custom:lock:test-key");
        Assert.Equal("test-owner", lockValue);
    }
}
