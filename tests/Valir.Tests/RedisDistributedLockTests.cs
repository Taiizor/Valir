using Moq;
using StackExchange.Redis;
using Valir.Redis;

namespace Valir.Tests;

/// <summary>
/// Unit tests for the RedisDistributedLock class.
/// </summary>
public class RedisDistributedLockTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private const string TestKey = "test-key";
    private const string TestOwner = "test-owner";
    private const string KeyPrefix = "valir:lock:";

    public RedisDistributedLockTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_mockDatabase.Object);

        // Default setup for StringSetAsync to return true (lock acquired)
        _mockDatabase
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task AcquireAsync_WhenLockNotHeld_ReturnsTrue()
    {
        // Arrange - Setup specific mock for this test (overrides constructor setup)
        _mockDatabase
            .Setup(d => d.StringSetAsync(
                KeyPrefix + TestKey,
                TestOwner,
                TimeSpan.FromSeconds(30),
                When.NotExists,
                CommandFlags.None))
            .ReturnsAsync(true);

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        bool result = await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AcquireAsync_WhenLockAlreadyHeldByAnother_ReturnsFalse()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        bool result = await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AcquireAsync_WhenLockAlreadyHeldBySameOwner_ReturnsFalse()
    {
        // Arrange - SET NX will fail even if same owner tries to acquire
        _mockDatabase
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        bool result = await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseAsync_WhenLockHeldByOwner_ReleasesLock()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        await lockInstance.ReleaseAsync();

        // Assert
        _mockDatabase.Verify(d => d.ScriptEvaluateAsync(
            It.Is<string>(s => s.Contains("redis.call('GET', KEYS[1])")),
            It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == KeyPrefix + TestKey),
            It.Is<RedisValue[]>(values => values.Length == 1 && values[0] == TestOwner),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_WhenLockHeldByAnother_DoesNotRelease()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0L));

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        await lockInstance.ReleaseAsync();

        // Assert - Verify the script was called but it returned 0 (didn't delete)
        _mockDatabase.Verify(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_WhenLockDoesNotExist_DoesNothing()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0L));

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        await lockInstance.ReleaseAsync();

        // Assert - Script is called but returns 0 since key doesn't exist
        _mockDatabase.Verify(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ExtendAsync_WhenLockHeldByOwner_ReturnsTrue()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        bool result = await lockInstance.ExtendAsync(TimeSpan.FromSeconds(60));

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(d => d.ScriptEvaluateAsync(
            It.Is<string>(s => s.Contains("PEXPIRE")),
            It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == KeyPrefix + TestKey),
            It.Is<RedisValue[]>(values => values.Length == 2 && values[0] == TestOwner && values[1] == 60000L),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ExtendAsync_WhenLockHeldByAnother_ReturnsFalse()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0L));

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        bool result = await lockInstance.ExtendAsync(TimeSpan.FromSeconds(60));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtendAsync_WhenLockDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0L));

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        bool result = await lockInstance.ExtendAsync(TimeSpan.FromSeconds(60));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DisposeAsync_WhenLockHeld_ReleasesLock()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        await lockInstance.DisposeAsync();

        // Assert
        _mockDatabase.Verify(d => d.ScriptEvaluateAsync(
            It.Is<string>(s => s.Contains("DEL")),
            It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == KeyPrefix + TestKey),
            It.Is<RedisValue[]>(values => values.Length == 1 && values[0] == TestOwner),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_ReleasesLockOnlyOnce()
    {
        // Arrange
        _mockDatabase
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Act
        await lockInstance.DisposeAsync();
        await lockInstance.DisposeAsync();
        await lockInstance.DisposeAsync();

        // Assert - Release should only be called once due to _disposed flag
        _mockDatabase.Verify(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void Constructor_SetsKeyAndOwnerProperties()
    {
        // Act
        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner);

        // Assert
        Assert.Equal(TestKey, lockInstance.Key);
        Assert.Equal(TestOwner, lockInstance.Owner);
    }

    [Fact]
    public void Constructor_WithCustomKeyPrefix_UsesCustomPrefix()
    {
        // Arrange
        const string customPrefix = "custom:lock:";

        // Act
        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner, customPrefix);

        // Assert
        Assert.Equal(TestKey, lockInstance.Key);
        Assert.Equal(TestOwner, lockInstance.Owner);
    }

    [Fact]
    public async Task AcquireAsync_WithCustomKeyPrefix_UsesCustomPrefix()
    {
        // Arrange
        const string customPrefix = "custom:lock:";
        _mockDatabase
            .Setup(d => d.StringSetAsync(
                customPrefix + TestKey,
                TestOwner,
                TimeSpan.FromSeconds(30),
                When.NotExists,
                CommandFlags.None))
            .ReturnsAsync(true);

        RedisDistributedLock lockInstance = new(_mockRedis.Object, TestKey, TestOwner, customPrefix);

        // Act
        bool result = await lockInstance.AcquireAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(d => d.StringSetAsync(
            customPrefix + TestKey,
            TestOwner,
            TimeSpan.FromSeconds(30),
            When.NotExists,
            CommandFlags.None), Times.Once);
    }
}
