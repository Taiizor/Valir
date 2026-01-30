using Moq;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using Valir.Abstractions;
using Valir.Extensions.Serilog;

namespace Valir.Tests;

/// <summary>
/// Unit tests for ValirSerilogLogger.
/// Tests the logger implementation for job execution logging.
/// </summary>
public class ValirSerilogLoggerTests
{
    private readonly Mock<ILogger> _serilogMock;
    private readonly SerilogOptions _options;
    private readonly JobContextEnricher _enricher;
    private readonly ValirSerilogLogger _logger;
    private readonly JobContext _jobContext;

    public ValirSerilogLoggerTests()
    {
        _serilogMock = new Mock<ILogger>();
        _options = new SerilogOptions();
        _enricher = new JobContextEnricher(_options);
        _logger = new ValirSerilogLogger(_serilogMock.Object, _options, _enricher);
        _jobContext = new JobContext("test-job-id", "worker-1", "lock-token-123", CancellationToken.None);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ValirSerilogLogger(null!, _options, _enricher));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ValirSerilogLogger(_serilogMock.Object, null!, _enricher));
    }

    [Fact]
    public void Constructor_WithNullEnricher_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ValirSerilogLogger(_serilogMock.Object, _options, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        ValirSerilogLogger logger = new(_serilogMock.Object, _options, _enricher);
        Assert.NotNull(logger);
    }

    #endregion

    #region LogJobStart Tests

    [Fact]
    public void LogJobStart_WithEnabledLogLevel_WritesLog()
    {
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Information)).Returns(true);

        _logger.LogJobStart("TestJob", _jobContext, 1);

        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Information,
            "Job {JobName} started (Attempt {Attempt})",
            It.Is<object[]>(args =>
                args[0].ToString() == "TestJob" &&
                args[1].ToString() == "1")),
            Times.Once);
    }

    [Fact]
    public void LogJobStart_WithDisabledLogLevel_DoesNotWriteLog()
    {
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Information)).Returns(false);

        _logger.LogJobStart("TestJob", _jobContext, 1);

        _serilogMock.Verify(x => x.Write(
            It.IsAny<LogEventLevel>(),
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void LogJobStart_WithCustomLogLevel_UsesConfiguredLevel()
    {
        _options.JobStartLogLevel = LogEventLevel.Debug;
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Debug)).Returns(true);

        _logger.LogJobStart("TestJob", _jobContext, 1);

        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Debug,
            "Job {JobName} started (Attempt {Attempt})",
            It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public void LogJobStart_SetsContextOnEnricher()
    {
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        _logger.LogJobStart("TestJob", _jobContext, 2);

        Assert.Same(_jobContext, _enricher.CurrentContext);
        Assert.Equal("TestJob", _enricher.CurrentJobName);
        Assert.Equal(2, _enricher.CurrentAttempt);
    }

    [Fact]
    public void LogJobStart_WithJobTypeFilterExcluded_DoesNotWriteLog()
    {
        _options.JobTypeFilter = jobName => jobName != "FilteredJob";
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        _logger.LogJobStart("FilteredJob", _jobContext, 1);

        _serilogMock.Verify(x => x.Write(
            It.IsAny<LogEventLevel>(),
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    #endregion

    #region LogJobComplete Tests

    [Fact]
    public void LogJobComplete_WithEnabledLogLevel_WritesLog()
    {
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Information)).Returns(true);
        Stopwatch stopwatch = Stopwatch.StartNew();
        stopwatch.Stop();

        _logger.LogJobComplete("TestJob", _jobContext, 1, stopwatch);

        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Information,
            "Job {JobName} completed in {DurationMs}ms (Attempt {Attempt})",
            It.Is<object[]>(args =>
                args[0].ToString() == "TestJob" &&
                args[2].ToString() == "1")),
            Times.Once);
    }

    [Fact]
    public void LogJobComplete_WithDisabledLogLevel_DoesNotWriteLog()
    {
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Information)).Returns(false);
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogJobComplete("TestJob", _jobContext, 1, stopwatch);

        _serilogMock.Verify(x => x.Write(
            It.IsAny<LogEventLevel>(),
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void LogJobComplete_WithTimingDisabled_WritesLogWithoutDuration()
    {
        _options.IncludeTiming = false;
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Information)).Returns(true);
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogJobComplete("TestJob", _jobContext, 1, stopwatch);

        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Information,
            "Job {JobName} completed (Attempt {Attempt})",
            It.Is<object[]>(args =>
                args[0].ToString() == "TestJob" &&
                args[1].ToString() == "1")),
            Times.Once);
    }

    [Fact]
    public void LogJobComplete_ClearsContextAfterLogging()
    {
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogJobComplete("TestJob", _jobContext, 1, stopwatch);

        Assert.Null(_enricher.CurrentContext);
    }

    [Fact]
    public void LogJobComplete_WithJobTypeFilterExcluded_DoesNotWriteLog()
    {
        _options.JobTypeFilter = jobName => jobName != "FilteredJob";
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogJobComplete("FilteredJob", _jobContext, 1, stopwatch);

        _serilogMock.Verify(x => x.Write(
            It.IsAny<LogEventLevel>(),
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    #endregion

    #region LogJobFailure Tests

    [Fact]
    public void LogJobFailure_WithEnabledLogLevel_WritesLog()
    {
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Error)).Returns(true);
        InvalidOperationException exception = new("Test error");
        Stopwatch stopwatch = Stopwatch.StartNew();
        stopwatch.Stop();

        _logger.LogJobFailure("TestJob", _jobContext, 1, exception, stopwatch);

        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Error,
            exception,
            "Job {JobName} failed after {DurationMs}ms (Attempt {Attempt})",
            It.Is<object[]>(args =>
                args[0].ToString() == "TestJob" &&
                args[2].ToString() == "1")),
            Times.Once);
    }

    [Fact]
    public void LogJobFailure_WithDisabledLogLevel_DoesNotWriteLog()
    {
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Error)).Returns(false);
        InvalidOperationException exception = new("Test error");
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogJobFailure("TestJob", _jobContext, 1, exception, stopwatch);

        _serilogMock.Verify(x => x.Write(
            It.IsAny<LogEventLevel>(),
            It.IsAny<Exception>(),
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void LogJobFailure_WithTimingDisabled_WritesLogWithoutDuration()
    {
        _options.IncludeTiming = false;
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Error)).Returns(true);
        InvalidOperationException exception = new("Test error");

        _logger.LogJobFailure("TestJob", _jobContext, 1, exception, null);

        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Error,
            exception,
            "Job {JobName} failed (Attempt {Attempt})",
            It.Is<object[]>(args =>
                args[0].ToString() == "TestJob" &&
                args[1].ToString() == "1")),
            Times.Once);
    }

    [Fact]
    public void LogJobFailure_ClearsContextAfterLogging()
    {
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        InvalidOperationException exception = new("Test error");
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogJobFailure("TestJob", _jobContext, 1, exception, stopwatch);

        Assert.Null(_enricher.CurrentContext);
    }

    [Fact]
    public void LogJobFailure_WithJobTypeFilterExcluded_DoesNotWriteLog()
    {
        _options.JobTypeFilter = jobName => jobName != "FilteredJob";
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        InvalidOperationException exception = new("Test error");
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogJobFailure("FilteredJob", _jobContext, 1, exception, stopwatch);

        _serilogMock.Verify(x => x.Write(
            It.IsAny<LogEventLevel>(),
            It.IsAny<Exception>(),
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    #endregion

    #region LogJobRetry Tests

    [Fact]
    public void LogJobRetry_WithEnabledLogLevel_WritesLog()
    {
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Warning)).Returns(true);
        InvalidOperationException exception = new("Retry error");

        _logger.LogJobRetry("TestJob", _jobContext, 2, exception);

        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Warning,
            exception,
            "Job {JobName} will be retried (Attempt {Attempt})",
            It.Is<object[]>(args =>
                args[0].ToString() == "TestJob" &&
                args[1].ToString() == "2")),
            Times.Once);
    }

    [Fact]
    public void LogJobRetry_WithDisabledLogLevel_DoesNotWriteLog()
    {
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Warning)).Returns(false);
        InvalidOperationException exception = new("Retry error");

        _logger.LogJobRetry("TestJob", _jobContext, 2, exception);

        _serilogMock.Verify(x => x.Write(
            It.IsAny<LogEventLevel>(),
            It.IsAny<Exception>(),
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void LogJobRetry_SetsContextOnEnricher()
    {
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        InvalidOperationException exception = new("Retry error");

        _logger.LogJobRetry("TestJob", _jobContext, 3, exception);

        Assert.Same(_jobContext, _enricher.CurrentContext);
        Assert.Equal("TestJob", _enricher.CurrentJobName);
        Assert.Equal(3, _enricher.CurrentAttempt);
    }

    [Fact]
    public void LogJobRetry_WithJobTypeFilterExcluded_DoesNotWriteLog()
    {
        _options.JobTypeFilter = jobName => jobName != "FilteredJob";
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        InvalidOperationException exception = new("Retry error");

        _logger.LogJobRetry("FilteredJob", _jobContext, 2, exception);

        _serilogMock.Verify(x => x.Write(
            It.IsAny<LogEventLevel>(),
            It.IsAny<Exception>(),
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    #endregion

    #region LogJobPayload Tests

    [Fact]
    public void LogJobPayload_WithEnabledLogging_WritesDebugLog()
    {
        _options.LogJobPayload = true;
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Debug)).Returns(true);
        TestPayload job = new() { Id = 1, Name = "Test" };

        _logger.LogJobPayload("TestJob", _jobContext, job);

        _serilogMock.Verify(x => x.Debug(
            "Job {JobName} payload: {Payload}",
            It.Is<object[]>(args =>
                args[0].ToString() == "TestJob" &&
                args[1].ToString()!.Contains("Test"))),
            Times.Once);
    }

    [Fact]
    public void LogJobPayload_WithPayloadLoggingDisabled_DoesNotWriteLog()
    {
        _options.LogJobPayload = false;
        TestPayload job = new() { Id = 1, Name = "Test" };

        _logger.LogJobPayload("TestJob", _jobContext, job);

        _serilogMock.Verify(x => x.Debug(
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void LogJobPayload_WithJobTypeFilterExcluded_DoesNotWriteLog()
    {
        _options.LogJobPayload = true;
        _options.JobTypeFilter = jobName => jobName != "FilteredJob";
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Debug)).Returns(true);
        TestPayload job = new() { Id = 1, Name = "Test" };

        _logger.LogJobPayload("FilteredJob", _jobContext, job);

        _serilogMock.Verify(x => x.Debug(
            It.IsAny<string>(),
            It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void LogJobPayload_WithLongPayload_TruncatesToMaxLength()
    {
        _options.LogJobPayload = true;
        _options.MaxPayloadLogLength = 10;
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Debug)).Returns(true);
        TestPayload job = new() { Id = 12345, Name = "VeryLongNameThatExceedsLimit" };

        _logger.LogJobPayload("TestJob", _jobContext, job);

        _serilogMock.Verify(x => x.Debug(
            "Job {JobName} payload: {Payload}",
            It.Is<object[]>(args =>
                args[1].ToString()!.Contains("... [truncated]") &&
                args[1].ToString()!.Length <= 25)), // 10 + "... [truncated]"
            Times.Once);
    }

    [Fact]
    public void LogJobPayload_WithSerializationError_WritesWarningLog()
    {
        _options.LogJobPayload = true;
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Debug)).Returns(true);
        NonSerializablePayload job = new();

        _logger.LogJobPayload("TestJob", _jobContext, job);

        _serilogMock.Verify(x => x.Warning(
            It.IsAny<Exception>(),
            "Failed to serialize job {JobName} payload for logging",
            It.Is<object[]>(args => args[0].ToString() == "TestJob")),
            Times.Once);
    }

    #endregion

    #region ForContext Tests

    [Fact]
    public void ForContext_SetsContextOnEnricher()
    {
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        _logger.ForContext(_jobContext, "TestJob", 1);

        Assert.Same(_jobContext, _enricher.CurrentContext);
        Assert.Equal("TestJob", _enricher.CurrentJobName);
        Assert.Equal(1, _enricher.CurrentAttempt);
    }

    [Fact]
    public void ForContext_ReturnsSerilogLogger()
    {
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        ILogger result = _logger.ForContext(_jobContext, "TestJob", 1);

        Assert.Same(_serilogMock.Object, result);
    }

    [Fact]
    public void ForContext_WithDefaultAttempt_UsesZero()
    {
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        _logger.ForContext(_jobContext, "TestJob");

        Assert.Equal(0, _enricher.CurrentAttempt);
    }

    #endregion

    #region ClearContext Tests

    [Fact]
    public void ClearContext_CallsEnricherClearContext()
    {
        _enricher.SetContext(_jobContext, "TestJob", 1);

        _logger.ClearContext();

        Assert.Null(_enricher.CurrentContext);
        Assert.Null(_enricher.CurrentJobName);
        Assert.Equal(0, _enricher.CurrentAttempt);
    }

    #endregion

    /// <summary>
    /// Test payload for serialization tests.
    /// </summary>
    public class TestPayload
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Non-serializable payload for testing error handling.
    /// </summary>
    public class NonSerializablePayload
    {
        public int Id { get; set; }
        public Stream BadProperty => throw new InvalidOperationException("Cannot serialize stream");
    }
}
