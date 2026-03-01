using Serilog.Core;
using Serilog.Events;
using Valir.Abstractions;
using Valir.Extensions.Serilog;

namespace Valir.Tests;

/// <summary>
/// Unit tests for JobContextEnricher.
/// Tests the enricher's ability to add job context properties to log events.
/// </summary>
public class JobContextEnricherTests
{
    private readonly SerilogOptions _options;
    private readonly JobContextEnricher _enricher;
    private readonly JobContext _jobContext;

    public JobContextEnricherTests()
    {
        _options = new SerilogOptions();
        _enricher = new JobContextEnricher(_options);
        _jobContext = new JobContext("test-job-id", "worker-1", "lock-token-123", CancellationToken.None);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new JobContextEnricher(null!));
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        // Act
        JobContextEnricher enricher = new(_options);

        // Assert
        Assert.NotNull(enricher);
    }

    #endregion

    #region Context Property Tests

    [Fact]
    public void CurrentContext_SetAndGet_ReturnsSetValue()
    {
        // Arrange
        JobContext context = new("job-1", "worker-1", "token-1", CancellationToken.None);

        // Act
        _enricher.CurrentContext = context;
        JobContext retrieved = _enricher.CurrentContext;

        // Assert
        Assert.Same(context, retrieved);
    }

    [Fact]
    public void CurrentContext_DefaultValue_IsNull()
    {
        // Act
        JobContext? context = _enricher.CurrentContext;

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public void CurrentJobName_SetAndGet_ReturnsSetValue()
    {
        // Arrange
        const string jobName = "TestJob";

        // Act
        _enricher.CurrentJobName = jobName;
        string retrieved = _enricher.CurrentJobName;

        // Assert
        Assert.Equal(jobName, retrieved);
    }

    [Fact]
    public void CurrentJobName_DefaultValue_IsNull()
    {
        // Act
        string? jobName = _enricher.CurrentJobName;

        // Assert
        Assert.Null(jobName);
    }

    [Fact]
    public void CurrentAttempt_SetAndGet_ReturnsSetValue()
    {
        // Arrange
        const int attempt = 3;

        // Act
        _enricher.CurrentAttempt = attempt;
        int retrieved = _enricher.CurrentAttempt;

        // Assert
        Assert.Equal(attempt, retrieved);
    }

    [Fact]
    public void CurrentAttempt_DefaultValue_IsZero()
    {
        // Act
        int attempt = _enricher.CurrentAttempt;

        // Assert
        Assert.Equal(0, attempt);
    }

    #endregion

    #region Enrich Tests - JobId

    [Fact]
    public void Enrich_WithJobContext_AddsJobIdProperty()
    {
        // Arrange
        _enricher.SetContext(_jobContext, "TestJob", 1);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("JobId"));
        Assert.Equal("test-job-id", logEvent.Properties["JobId"].ToString().Trim('"'));
    }

    [Fact]
    public void Enrich_WithCustomJobIdPropertyName_UsesConfiguredName()
    {
        // Arrange
        _options.JobIdPropertyName = "CustomJobId";
        _enricher.SetContext(_jobContext, "TestJob", 1);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("CustomJobId"));
        Assert.False(logEvent.Properties.ContainsKey("JobId"));
    }

    #endregion

    #region Enrich Tests - WorkerId

    [Fact]
    public void Enrich_WithJobContext_AddsWorkerIdProperty()
    {
        // Arrange
        _enricher.SetContext(_jobContext, "TestJob", 1);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("WorkerId"));
        Assert.Equal("worker-1", logEvent.Properties["WorkerId"].ToString().Trim('"'));
    }

    [Fact]
    public void Enrich_WithCustomWorkerIdPropertyName_UsesConfiguredName()
    {
        // Arrange
        _options.WorkerIdPropertyName = "CustomWorkerId";
        _enricher.SetContext(_jobContext, "TestJob", 1);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("CustomWorkerId"));
        Assert.False(logEvent.Properties.ContainsKey("WorkerId"));
    }

    #endregion

    #region Enrich Tests - JobName

    [Fact]
    public void Enrich_WithJobName_AddsJobNameProperty()
    {
        // Arrange
        _enricher.SetContext(_jobContext, "MyTestJob", 1);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("JobName"));
        Assert.Equal("MyTestJob", logEvent.Properties["JobName"].ToString().Trim('"'));
    }

    [Fact]
    public void Enrich_WithNullJobName_DoesNotAddJobNameProperty()
    {
        // Arrange
        _enricher.SetContext(_jobContext, null!, 1);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("JobName"));
    }

    [Fact]
    public void Enrich_WithEmptyJobName_DoesNotAddJobNameProperty()
    {
        // Arrange
        _enricher.SetContext(_jobContext, "", 1);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("JobName"));
    }

    [Fact]
    public void Enrich_WithCustomJobNamePropertyName_UsesConfiguredName()
    {
        // Arrange
        _options.JobNamePropertyName = "CustomJobName";
        _enricher.SetContext(_jobContext, "TestJob", 1);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("CustomJobName"));
        Assert.False(logEvent.Properties.ContainsKey("JobName"));
    }

    #endregion

    #region Enrich Tests - Attempt

    [Fact]
    public void Enrich_WithAttemptGreaterThanZero_AddsAttemptProperty()
    {
        // Arrange
        _enricher.SetContext(_jobContext, "TestJob", 3);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("Attempt"));
        Assert.Equal("3", logEvent.Properties["Attempt"].ToString());
    }

    [Fact]
    public void Enrich_WithAttemptZero_DoesNotAddAttemptProperty()
    {
        // Arrange
        _enricher.SetContext(_jobContext, "TestJob", 0);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("Attempt"));
    }

    [Fact]
    public void Enrich_WithCustomAttemptPropertyName_UsesConfiguredName()
    {
        // Arrange
        _options.AttemptPropertyName = "CustomAttempt";
        _enricher.SetContext(_jobContext, "TestJob", 2);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("CustomAttempt"));
        Assert.False(logEvent.Properties.ContainsKey("Attempt"));
    }

    #endregion

    #region Enrich Tests - Context Disabled

    [Fact]
    public void Enrich_WithEnrichWithJobContextDisabled_DoesNotAddProperties()
    {
        // Arrange
        _options.EnrichWithJobContext = false;
        _enricher.SetContext(_jobContext, "TestJob", 1);
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("JobId"));
        Assert.False(logEvent.Properties.ContainsKey("WorkerId"));
        Assert.False(logEvent.Properties.ContainsKey("JobName"));
        Assert.False(logEvent.Properties.ContainsKey("Attempt"));
    }

    #endregion

    #region Enrich Tests - No Context

    [Fact]
    public void Enrich_WithNoContextSet_DoesNotAddProperties()
    {
        // Arrange
        LogEvent logEvent = CreateLogEvent();
        MockPropertyFactory propertyFactory = new();

        // Act
        _enricher.Enrich(logEvent, propertyFactory);

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("JobId"));
        Assert.False(logEvent.Properties.ContainsKey("WorkerId"));
    }

    #endregion

    #region SetContext Tests

    [Fact]
    public void SetContext_SetsAllContextValues()
    {
        // Arrange
        JobContext context = new("job-123", "worker-2", "token-456", CancellationToken.None);
        const string jobName = "MyJob";
        const int attempt = 5;

        // Act
        _enricher.SetContext(context, jobName, attempt);

        // Assert
        Assert.Same(context, _enricher.CurrentContext);
        Assert.Equal(jobName, _enricher.CurrentJobName);
        Assert.Equal(attempt, _enricher.CurrentAttempt);
    }

    #endregion

    #region ClearContext Tests

    [Fact]
    public void ClearContext_ClearsAllContextValues()
    {
        // Arrange
        JobContext context = new("job-123", "worker-2", "token-456", CancellationToken.None);
        _enricher.SetContext(context, "MyJob", 5);

        // Act
        _enricher.ClearContext();

        // Assert
        Assert.Null(_enricher.CurrentContext);
        Assert.Null(_enricher.CurrentJobName);
        Assert.Equal(0, _enricher.CurrentAttempt);
    }

    #endregion

    #region Helper Methods

    private static LogEvent CreateLogEvent()
    {
        return new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            null,
            new MessageTemplate("Test message", []),
            []);
    }

    /// <summary>
    /// Mock implementation of ILogEventPropertyFactory for testing.
    /// </summary>
    private class MockPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            ScalarValue scalarValue = value is null
                ? new ScalarValue(null)
                : new ScalarValue(value);
            return new LogEventProperty(name, scalarValue);
        }
    }

    #endregion
}
