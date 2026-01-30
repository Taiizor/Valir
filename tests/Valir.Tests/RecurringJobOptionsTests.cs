using Valir.Abstractions;

namespace Valir.Tests;

/// <summary>
/// Unit tests for RecurringJobOptions class.
/// Tests default values and property setters.
/// </summary>
public class RecurringJobOptionsTests
{
    #region Default Value Tests

    [Fact]
    public void DefaultOptions_HaveCorrectDefaultValues()
    {
        // Arrange & Act
        var options = new RecurringJobOptions();

        // Assert
        Assert.Equal(CronFormat.Standard, options.CronFormat);
        Assert.Null(options.TimeZone);
        Assert.Equal("default", options.Queue);
        Assert.Equal(0, options.Priority);
        Assert.Equal(MisfirePolicy.FireOnce, options.MisfirePolicy);
        Assert.Equal(3, options.MaxRetries);
    }

    [Fact]
    public void DefaultOptions_CronFormat_IsStandard()
    {
        // Arrange & Act
        var options = new RecurringJobOptions();

        // Assert
        Assert.Equal(CronFormat.Standard, options.CronFormat);
    }

    [Fact]
    public void DefaultOptions_TimeZone_IsNull()
    {
        // Arrange & Act
        var options = new RecurringJobOptions();

        // Assert
        Assert.Null(options.TimeZone);
    }

    [Fact]
    public void DefaultOptions_Queue_IsDefault()
    {
        // Arrange & Act
        var options = new RecurringJobOptions();

        // Assert
        Assert.Equal("default", options.Queue);
    }

    [Fact]
    public void DefaultOptions_Priority_IsZero()
    {
        // Arrange & Act
        var options = new RecurringJobOptions();

        // Assert
        Assert.Equal(0, options.Priority);
    }

    [Fact]
    public void DefaultOptions_MisfirePolicy_IsFireOnce()
    {
        // Arrange & Act
        var options = new RecurringJobOptions();

        // Assert
        Assert.Equal(MisfirePolicy.FireOnce, options.MisfirePolicy);
    }

    [Fact]
    public void DefaultOptions_MaxRetries_IsThree()
    {
        // Arrange & Act
        var options = new RecurringJobOptions();

        // Assert
        Assert.Equal(3, options.MaxRetries);
    }

    #endregion

    #region Property Setter Tests

    [Fact]
    public void CronFormat_CanBeSetToIncludeSeconds()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            CronFormat = CronFormat.IncludeSeconds
        };

        // Assert
        Assert.Equal(CronFormat.IncludeSeconds, options.CronFormat);
    }

    [Fact]
    public void TimeZone_CanBeSet()
    {
        // Arrange
        var options = new RecurringJobOptions();
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        // Act
        options.TimeZone = timeZone;

        // Assert
        Assert.Equal(timeZone, options.TimeZone);
    }

    [Fact]
    public void TimeZone_CanBeSetToNull()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        };

        // Act
        options.TimeZone = null;

        // Assert
        Assert.Null(options.TimeZone);
    }

    [Fact]
    public void Queue_CanBeSet()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            Queue = "custom-queue"
        };

        // Assert
        Assert.Equal("custom-queue", options.Queue);
    }

    [Fact]
    public void Queue_CanBeSetToEmptyString()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            Queue = ""
        };

        // Assert
        Assert.Empty(options.Queue);
    }

    [Fact]
    public void Priority_CanBeSetToPositiveValue()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            Priority = 10
        };

        // Assert
        Assert.Equal(10, options.Priority);
    }

    [Fact]
    public void Priority_CanBeSetToNegativeValue()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            Priority = -5
        };

        // Assert
        Assert.Equal(-5, options.Priority);
    }

    [Fact]
    public void MisfirePolicy_CanBeSetToSkip()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            MisfirePolicy = MisfirePolicy.Skip
        };

        // Assert
        Assert.Equal(MisfirePolicy.Skip, options.MisfirePolicy);
    }

    [Fact]
    public void MisfirePolicy_CanBeSetToFireAll()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            MisfirePolicy = MisfirePolicy.FireAll
        };

        // Assert
        Assert.Equal(MisfirePolicy.FireAll, options.MisfirePolicy);
    }

    [Fact]
    public void MisfirePolicy_CanBeSetToFireNow()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            MisfirePolicy = MisfirePolicy.FireNow
        };

        // Assert
        Assert.Equal(MisfirePolicy.FireNow, options.MisfirePolicy);
    }

    [Fact]
    public void MaxRetries_CanBeSetToZero()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            MaxRetries = 0
        };

        // Assert
        Assert.Equal(0, options.MaxRetries);
    }

    [Fact]
    public void MaxRetries_CanBeSetToHigherValue()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            MaxRetries = 10
        };

        // Assert
        Assert.Equal(10, options.MaxRetries);
    }

    #endregion

    #region Object Initialization Tests

    [Fact]
    public void Options_CanBeInitializedWithObjectInitializer()
    {
        // Arrange & Act
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var options = new RecurringJobOptions
        {
            CronFormat = CronFormat.IncludeSeconds,
            TimeZone = timeZone,
            Queue = "high-priority",
            Priority = 100,
            MisfirePolicy = MisfirePolicy.FireAll,
            MaxRetries = 5
        };

        // Assert
        Assert.Equal(CronFormat.IncludeSeconds, options.CronFormat);
        Assert.Equal(timeZone, options.TimeZone);
        Assert.Equal("high-priority", options.Queue);
        Assert.Equal(100, options.Priority);
        Assert.Equal(MisfirePolicy.FireAll, options.MisfirePolicy);
        Assert.Equal(5, options.MaxRetries);
    }

    [Fact]
    public void Options_CanBePartiallyInitialized()
    {
        // Arrange & Act
        var options = new RecurringJobOptions
        {
            Queue = "custom-queue",
            MaxRetries = 1
        };

        // Assert - Unset properties should have default values
        Assert.Equal(CronFormat.Standard, options.CronFormat);
        Assert.Null(options.TimeZone);
        Assert.Equal("custom-queue", options.Queue);
        Assert.Equal(0, options.Priority);
        Assert.Equal(MisfirePolicy.FireOnce, options.MisfirePolicy);
        Assert.Equal(1, options.MaxRetries);
    }

    #endregion

    #region Multiple Instance Tests

    [Fact]
    public void MultipleOptions_InstancesAreIndependent()
    {
        // Arrange
        var options1 = new RecurringJobOptions
        {
            Queue = "queue-1",
            Priority = 5
        };

        var options2 = new RecurringJobOptions
        {
            Queue = "queue-2",
            Priority = 10
        };

        // Assert
        Assert.Equal("queue-1", options1.Queue);
        Assert.Equal(5, options1.Priority);
        Assert.Equal("queue-2", options2.Queue);
        Assert.Equal(10, options2.Priority);
    }

    [Fact]
    public void ModifyingOneInstance_DoesNotAffectOther()
    {
        // Arrange
        var options1 = new RecurringJobOptions();
        var options2 = new RecurringJobOptions();

        // Act
        options1.Queue = "modified-queue";
        options1.Priority = 50;

        // Assert
        Assert.Equal("modified-queue", options1.Queue);
        Assert.Equal(50, options1.Priority);
        Assert.Equal("default", options2.Queue);
        Assert.Equal(0, options2.Priority);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Queue_CanContainSpecialCharacters()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            Queue = "queue-with-special.chars_123"
        };

        // Assert
        Assert.Equal("queue-with-special.chars_123", options.Queue);
    }

    [Fact]
    public void Priority_CanBeSetToMaxInt()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            Priority = int.MaxValue
        };

        // Assert
        Assert.Equal(int.MaxValue, options.Priority);
    }

    [Fact]
    public void Priority_CanBeSetToMinInt()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            Priority = int.MinValue
        };

        // Assert
        Assert.Equal(int.MinValue, options.Priority);
    }

    [Fact]
    public void MaxRetries_CanBeSetToMaxInt()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act
            MaxRetries = int.MaxValue
        };

        // Assert
        Assert.Equal(int.MaxValue, options.MaxRetries);
    }

    [Fact]
    public void CronFormat_CanBeToggledMultipleTimes()
    {
        // Arrange
        var options = new RecurringJobOptions
        {
            // Act & Assert
            CronFormat = CronFormat.IncludeSeconds
        };

        Assert.Equal(CronFormat.IncludeSeconds, options.CronFormat);

        options.CronFormat = CronFormat.Standard;
        Assert.Equal(CronFormat.Standard, options.CronFormat);

        options.CronFormat = CronFormat.IncludeSeconds;
        Assert.Equal(CronFormat.IncludeSeconds, options.CronFormat);
    }

    #endregion
}
