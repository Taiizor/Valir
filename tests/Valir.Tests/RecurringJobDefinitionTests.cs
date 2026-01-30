using Valir.Abstractions;

namespace Valir.Tests;

/// <summary>
/// Unit tests for RecurringJobDefinition record.
/// Tests property validation and constructor behavior.
/// </summary>
public class RecurringJobDefinitionTests
{
    #region Property Tests

    [Fact]
    public void RecurringJobDefinition_RequiredProperties_CanBeSet()
    {
        // Arrange & Act
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] payload = "test-payload"u8.ToArray();

        RecurringJobDefinition definition = new()
        {
            JobId = "test-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = payload,
            Queue = "default",
            Priority = 5,
            CronFormat = CronFormat.Standard,
            TimeZoneId = "UTC",
            MisfirePolicy = MisfirePolicy.FireOnce,
            MaxRetries = 3,
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now,
            NextExecution = now.AddMinutes(5),
            LastExecution = now.AddMinutes(-55)
        };

        // Assert
        Assert.Equal("test-job", definition.JobId);
        Assert.Equal("* * * * *", definition.CronExpression);
        Assert.Equal("TestJob", definition.JobType);
        Assert.Equal(payload, definition.Payload);
        Assert.Equal("default", definition.Queue);
        Assert.Equal(5, definition.Priority);
        Assert.Equal(CronFormat.Standard, definition.CronFormat);
        Assert.Equal("UTC", definition.TimeZoneId);
        Assert.Equal(MisfirePolicy.FireOnce, definition.MisfirePolicy);
        Assert.Equal(3, definition.MaxRetries);
        Assert.True(definition.Enabled);
        Assert.Equal(now, definition.CreatedAt);
        Assert.Equal(now, definition.UpdatedAt);
        Assert.Equal(now.AddMinutes(5), definition.NextExecution);
        Assert.Equal(now.AddMinutes(-55), definition.LastExecution);
    }

    [Fact]
    public void RecurringJobDefinition_NullTimeZoneId_Allowed()
    {
        // Arrange & Act
        RecurringJobDefinition definition = new()
        {
            JobId = "test-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = "payload"u8.ToArray(),
            Queue = "default",
            TimeZoneId = null
        };

        // Assert
        Assert.Null(definition.TimeZoneId);
    }

    [Fact]
    public void RecurringJobDefinition_NullNextExecution_Allowed()
    {
        // Arrange & Act
        RecurringJobDefinition definition = new()
        {
            JobId = "test-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = "payload"u8.ToArray(),
            Queue = "default",
            NextExecution = null
        };

        // Assert
        Assert.Null(definition.NextExecution);
    }

    [Fact]
    public void RecurringJobDefinition_NullLastExecution_Allowed()
    {
        // Arrange & Act
        RecurringJobDefinition definition = new()
        {
            JobId = "test-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = "payload"u8.ToArray(),
            Queue = "default",
            LastExecution = null
        };

        // Assert
        Assert.Null(definition.LastExecution);
    }

    #endregion

    #region Create Method Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsDefinition()
    {
        // Arrange
        string jobId = "test-create-job";
        string cronExpression = "*/5 * * * *";
        string jobType = "TestJob";
        byte[] payload = "test-payload"u8.ToArray();

        // Act
        RecurringJobDefinition definition = RecurringJobDefinition.Create(
            jobId,
            cronExpression,
            jobType,
            payload);

        // Assert
        Assert.NotNull(definition);
        Assert.Equal(jobId, definition.JobId);
        Assert.Equal(cronExpression, definition.CronExpression);
        Assert.Equal(jobType, definition.JobType);
        Assert.Equal(payload, definition.Payload);
        Assert.Equal("default", definition.Queue);
        Assert.Equal(0, definition.Priority);
        Assert.Equal(CronFormat.Standard, definition.CronFormat);
        Assert.Null(definition.TimeZoneId);
        Assert.Equal(MisfirePolicy.FireOnce, definition.MisfirePolicy);
        Assert.Equal(3, definition.MaxRetries);
        Assert.True(definition.Enabled);
        Assert.True(definition.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.Equal(definition.CreatedAt, definition.UpdatedAt);
    }

    [Fact]
    public void Create_WithCustomOptions_AppliesOptions()
    {
        // Arrange
        string jobId = "test-options-job";
        string cronExpression = "0 */6 * * *";
        string jobType = "TestJob";
        byte[] payload = "test-payload"u8.ToArray();
        RecurringJobOptions options = new()
        {
            Queue = "custom-queue",
            Priority = 10,
            CronFormat = CronFormat.IncludeSeconds,
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
            MisfirePolicy = MisfirePolicy.Skip,
            MaxRetries = 5
        };

        // Act
        RecurringJobDefinition definition = RecurringJobDefinition.Create(
            jobId,
            cronExpression,
            jobType,
            payload,
            options);

        // Assert
        Assert.Equal("custom-queue", definition.Queue);
        Assert.Equal(10, definition.Priority);
        Assert.Equal(CronFormat.IncludeSeconds, definition.CronFormat);
        Assert.Equal("Eastern Standard Time", definition.TimeZoneId);
        Assert.Equal(MisfirePolicy.Skip, definition.MisfirePolicy);
        Assert.Equal(5, definition.MaxRetries);
    }

    [Fact]
    public void Create_WithNullJobId_ThrowsArgumentException()
    {
        // Arrange
        byte[] payload = "payload"u8.ToArray();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RecurringJobDefinition.Create(null!, "* * * * *", "TestJob", payload));
    }

    [Fact]
    public void Create_WithEmptyJobId_ThrowsArgumentException()
    {
        // Arrange
        byte[] payload = "payload"u8.ToArray();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RecurringJobDefinition.Create("", "* * * * *", "TestJob", payload));
    }

    [Fact]
    public void Create_WithWhitespaceJobId_ThrowsArgumentException()
    {
        // Arrange
        byte[] payload = "payload"u8.ToArray();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RecurringJobDefinition.Create("   ", "* * * * *", "TestJob", payload));
    }

    [Fact]
    public void Create_WithNullCronExpression_ThrowsArgumentException()
    {
        // Arrange
        byte[] payload = "payload"u8.ToArray();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RecurringJobDefinition.Create("job-id", null!, "TestJob", payload));
    }

    [Fact]
    public void Create_WithEmptyCronExpression_ThrowsArgumentException()
    {
        // Arrange
        byte[] payload = "payload"u8.ToArray();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RecurringJobDefinition.Create("job-id", "", "TestJob", payload));
    }

    [Fact]
    public void Create_WithNullJobType_ThrowsArgumentException()
    {
        // Arrange
        byte[] payload = "payload"u8.ToArray();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RecurringJobDefinition.Create("job-id", "* * * * *", null!, payload));
    }

    [Fact]
    public void Create_WithEmptyJobType_ThrowsArgumentException()
    {
        // Arrange
        byte[] payload = "payload"u8.ToArray();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RecurringJobDefinition.Create("job-id", "* * * * *", "", payload));
    }

    [Fact]
    public void Create_WithNullPayload_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            RecurringJobDefinition.Create("job-id", "* * * * *", "TestJob", null!));
    }

    [Fact]
    public void Create_WithNullOptions_UsesDefaultOptions()
    {
        // Arrange
        byte[] payload = "payload"u8.ToArray();

        // Act
        RecurringJobDefinition definition = RecurringJobDefinition.Create(
            "job-id",
            "* * * * *",
            "TestJob",
            payload,
            null);

        // Assert
        Assert.NotNull(definition);
        Assert.Equal("default", definition.Queue);
        Assert.Equal(MisfirePolicy.FireOnce, definition.MisfirePolicy);
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void RecurringJobDefinition_IsImmutableRecord()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RecurringJobDefinition definition = new()
        {
            JobId = "original-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = "payload"u8.ToArray(),
            Queue = "default",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Act - Create modified copy using 'with' expression
        RecurringJobDefinition modified = definition with { JobId = "modified-job" };

        // Assert - Original should remain unchanged
        Assert.Equal("original-job", definition.JobId);
        Assert.Equal("modified-job", modified.JobId);
        Assert.Equal(definition.CronExpression, modified.CronExpression);
        Assert.Equal(definition.JobType, modified.JobType);
    }

    [Fact]
    public void RecurringJobDefinition_Equality_SameValuesAreEqual()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] payload = "payload"u8.ToArray();

        RecurringJobDefinition definition1 = new()
        {
            JobId = "test-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = payload,
            Queue = "default",
            CreatedAt = now,
            UpdatedAt = now
        };

        RecurringJobDefinition definition2 = new()
        {
            JobId = "test-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = payload,
            Queue = "default",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Act & Assert
        Assert.Equal(definition1, definition2);
        Assert.True(definition1 == definition2);
    }

    [Fact]
    public void RecurringJobDefinition_Equality_DifferentValuesAreNotEqual()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;

        RecurringJobDefinition definition1 = new()
        {
            JobId = "job-1",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = "payload"u8.ToArray(),
            Queue = "default",
            CreatedAt = now,
            UpdatedAt = now
        };

        RecurringJobDefinition definition2 = new()
        {
            JobId = "job-2",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = "payload"u8.ToArray(),
            Queue = "default",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Act & Assert
        Assert.NotEqual(definition1, definition2);
        Assert.True(definition1 != definition2);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void RecurringJobDefinition_EmptyPayload_Allowed()
    {
        // Arrange & Act
        RecurringJobDefinition definition = new()
        {
            JobId = "test-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = [],
            Queue = "default"
        };

        // Assert
        Assert.Empty(definition.Payload);
    }

    [Fact]
    public void RecurringJobDefinition_NegativePriority_Allowed()
    {
        // Arrange & Act
        RecurringJobDefinition definition = new()
        {
            JobId = "test-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = "payload"u8.ToArray(),
            Queue = "default",
            Priority = -5
        };

        // Assert
        Assert.Equal(-5, definition.Priority);
    }

    [Fact]
    public void RecurringJobDefinition_ZeroMaxRetries_Allowed()
    {
        // Arrange & Act
        RecurringJobDefinition definition = new()
        {
            JobId = "test-job",
            CronExpression = "* * * * *",
            JobType = "TestJob",
            Payload = "payload"u8.ToArray(),
            Queue = "default",
            MaxRetries = 0
        };

        // Assert
        Assert.Equal(0, definition.MaxRetries);
    }

    #endregion
}
