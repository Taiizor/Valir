using Serilog.Events;
using Valir.Extensions.Serilog;

namespace Valir.Tests;

/// <summary>
/// Unit tests for SerilogOptions.
/// Tests the configuration options class for Serilog integration.
/// </summary>
public class SerilogOptionsTests
{
    #region Default Value Tests

    [Fact]
    public void Default_MinimumLogLevel_IsInformation()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        LogEventLevel level = options.MinimumLogLevel;

        // Assert
        Assert.Equal(LogEventLevel.Information, level);
    }

    [Fact]
    public void Default_JobStartLogLevel_IsInformation()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        LogEventLevel level = options.JobStartLogLevel;

        // Assert
        Assert.Equal(LogEventLevel.Information, level);
    }

    [Fact]
    public void Default_JobCompleteLogLevel_IsInformation()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        LogEventLevel level = options.JobCompleteLogLevel;

        // Assert
        Assert.Equal(LogEventLevel.Information, level);
    }

    [Fact]
    public void Default_JobFailureLogLevel_IsError()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        LogEventLevel level = options.JobFailureLogLevel;

        // Assert
        Assert.Equal(LogEventLevel.Error, level);
    }

    [Fact]
    public void Default_JobRetryLogLevel_IsWarning()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        LogEventLevel level = options.JobRetryLogLevel;

        // Assert
        Assert.Equal(LogEventLevel.Warning, level);
    }

    [Fact]
    public void Default_EnrichWithJobContext_IsTrue()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        bool enrich = options.EnrichWithJobContext;

        // Assert
        Assert.True(enrich);
    }

    [Fact]
    public void Default_LogJobPayload_IsFalse()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        bool logPayload = options.LogJobPayload;

        // Assert
        Assert.False(logPayload);
    }

    [Fact]
    public void Default_MaxPayloadLogLength_Is1000()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        int length = options.MaxPayloadLogLength;

        // Assert
        Assert.Equal(1000, length);
    }

    [Fact]
    public void Default_IncludeTiming_IsTrue()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        bool includeTiming = options.IncludeTiming;

        // Assert
        Assert.True(includeTiming);
    }

    [Fact]
    public void Default_JobIdPropertyName_IsJobId()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        string name = options.JobIdPropertyName;

        // Assert
        Assert.Equal("JobId", name);
    }

    [Fact]
    public void Default_JobNamePropertyName_IsJobName()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        string name = options.JobNamePropertyName;

        // Assert
        Assert.Equal("JobName", name);
    }

    [Fact]
    public void Default_WorkerIdPropertyName_IsWorkerId()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        string name = options.WorkerIdPropertyName;

        // Assert
        Assert.Equal("WorkerId", name);
    }

    [Fact]
    public void Default_AttemptPropertyName_IsAttempt()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        string name = options.AttemptPropertyName;

        // Assert
        Assert.Equal("Attempt", name);
    }

    [Fact]
    public void Default_JobTypeFilter_IsNull()
    {
        // Arrange
        SerilogOptions options = new();

        // Act
        Func<string, bool>? filter = options.JobTypeFilter;

        // Assert
        Assert.Null(filter);
    }

    #endregion

    #region Property Setter Tests - Log Levels

    [Theory]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public void MinimumLogLevel_SetToAnyValue_UpdatesValue(LogEventLevel level)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            MinimumLogLevel = level
        };

        // Assert
        Assert.Equal(level, options.MinimumLogLevel);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public void JobStartLogLevel_SetToAnyValue_UpdatesValue(LogEventLevel level)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            JobStartLogLevel = level
        };

        // Assert
        Assert.Equal(level, options.JobStartLogLevel);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public void JobCompleteLogLevel_SetToAnyValue_UpdatesValue(LogEventLevel level)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            JobCompleteLogLevel = level
        };

        // Assert
        Assert.Equal(level, options.JobCompleteLogLevel);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public void JobFailureLogLevel_SetToAnyValue_UpdatesValue(LogEventLevel level)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            JobFailureLogLevel = level
        };

        // Assert
        Assert.Equal(level, options.JobFailureLogLevel);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public void JobRetryLogLevel_SetToAnyValue_UpdatesValue(LogEventLevel level)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            JobRetryLogLevel = level
        };

        // Assert
        Assert.Equal(level, options.JobRetryLogLevel);
    }

    #endregion

    #region Property Setter Tests - Boolean Options

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnrichWithJobContext_SetToAnyValue_UpdatesValue(bool value)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            EnrichWithJobContext = value
        };

        // Assert
        Assert.Equal(value, options.EnrichWithJobContext);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LogJobPayload_SetToAnyValue_UpdatesValue(bool value)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            LogJobPayload = value
        };

        // Assert
        Assert.Equal(value, options.LogJobPayload);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IncludeTiming_SetToAnyValue_UpdatesValue(bool value)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            IncludeTiming = value
        };

        // Assert
        Assert.Equal(value, options.IncludeTiming);
    }

    #endregion

    #region Property Setter Tests - String Options

    [Theory]
    [InlineData("CustomJobId")]
    [InlineData("JobIdentifier")]
    [InlineData("Id")]
    public void JobIdPropertyName_SetToAnyValue_UpdatesValue(string name)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            JobIdPropertyName = name
        };

        // Assert
        Assert.Equal(name, options.JobIdPropertyName);
    }

    [Theory]
    [InlineData("CustomJobName")]
    [InlineData("JobType")]
    [InlineData("Name")]
    public void JobNamePropertyName_SetToAnyValue_UpdatesValue(string name)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            JobNamePropertyName = name
        };

        // Assert
        Assert.Equal(name, options.JobNamePropertyName);
    }

    [Theory]
    [InlineData("CustomWorkerId")]
    [InlineData("WorkerIdentifier")]
    [InlineData("InstanceId")]
    public void WorkerIdPropertyName_SetToAnyValue_UpdatesValue(string name)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            WorkerIdPropertyName = name
        };

        // Assert
        Assert.Equal(name, options.WorkerIdPropertyName);
    }

    [Theory]
    [InlineData("CustomAttempt")]
    [InlineData("RetryCount")]
    [InlineData("TryNumber")]
    public void AttemptPropertyName_SetToAnyValue_UpdatesValue(string name)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            AttemptPropertyName = name
        };

        // Assert
        Assert.Equal(name, options.AttemptPropertyName);
    }

    #endregion

    #region Property Setter Tests - Integer Options

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void MaxPayloadLogLength_SetToAnyValue_UpdatesValue(int length)
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            MaxPayloadLogLength = length
        };

        // Assert
        Assert.Equal(length, options.MaxPayloadLogLength);
    }

    [Fact]
    public void MaxPayloadLogLength_CanBeSetToZero()
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            MaxPayloadLogLength = 0
        };

        // Assert
        Assert.Equal(0, options.MaxPayloadLogLength);
    }

    [Fact]
    public void MaxPayloadLogLength_CanBeSetToNegative()
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            MaxPayloadLogLength = -1
        };

        // Assert
        Assert.Equal(-1, options.MaxPayloadLogLength);
    }

    #endregion

    #region Property Setter Tests - Filter

    [Fact]
    public void JobTypeFilter_SetToFilterFunction_UpdatesValue()
    {
        // Arrange
        SerilogOptions options = new();
        Func<string, bool> filter = jobType => jobType.StartsWith("Important");

        // Act
        options.JobTypeFilter = filter;

        // Assert
        Assert.Same(filter, options.JobTypeFilter);
    }

    [Fact]
    public void JobTypeFilter_SetToNull_ClearsFilter()
    {
        // Arrange
        SerilogOptions options = new()
        {
            JobTypeFilter = _ => true
        };

        // Act
        options.JobTypeFilter = null;

        // Assert
        Assert.Null(options.JobTypeFilter);
    }

    [Fact]
    public void JobTypeFilter_FilterFunction_WorksCorrectly()
    {
        // Arrange
        SerilogOptions options = new()
        {
            JobTypeFilter = jobType => jobType.Contains("Test")
        };

        // Act & Assert
        Assert.True(options.JobTypeFilter!("MyTestJob"));
        Assert.False(options.JobTypeFilter!("MyProductionJob"));
    }

    #endregion

    #region Complex Configuration Tests

    [Fact]
    public void MultipleProperties_CanBeConfiguredTogether()
    {
        // Arrange
        SerilogOptions options = new()
        {
            // Act
            MinimumLogLevel = LogEventLevel.Debug,
            JobStartLogLevel = LogEventLevel.Verbose,
            JobFailureLogLevel = LogEventLevel.Fatal,
            EnrichWithJobContext = true,
            LogJobPayload = true,
            IncludeTiming = false,
            JobIdPropertyName = "CorrelationId",
            MaxPayloadLogLength = 500,
            JobTypeFilter = jobType => !jobType.Contains("Sensitive")
        };

        // Assert
        Assert.Equal(LogEventLevel.Debug, options.MinimumLogLevel);
        Assert.Equal(LogEventLevel.Verbose, options.JobStartLogLevel);
        Assert.Equal(LogEventLevel.Fatal, options.JobFailureLogLevel);
        Assert.True(options.EnrichWithJobContext);
        Assert.True(options.LogJobPayload);
        Assert.False(options.IncludeTiming);
        Assert.Equal("CorrelationId", options.JobIdPropertyName);
        Assert.Equal(500, options.MaxPayloadLogLength);
        Assert.NotNull(options.JobTypeFilter);
        Assert.True(options.JobTypeFilter!("RegularJob"));
        Assert.False(options.JobTypeFilter!("SensitiveJob"));
    }

    [Fact]
    public void IndependentInstances_HaveIndependentValues()
    {
        // Arrange
        SerilogOptions options1 = new();
        SerilogOptions options2 = new();

        // Act
        options1.MinimumLogLevel = LogEventLevel.Debug;
        options1.JobIdPropertyName = "CustomId";
        options2.MinimumLogLevel = LogEventLevel.Error;
        options2.JobIdPropertyName = "AnotherId";

        // Assert
        Assert.Equal(LogEventLevel.Debug, options1.MinimumLogLevel);
        Assert.Equal("CustomId", options1.JobIdPropertyName);
        Assert.Equal(LogEventLevel.Error, options2.MinimumLogLevel);
        Assert.Equal("AnotherId", options2.JobIdPropertyName);
    }

    #endregion
}
