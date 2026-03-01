using Moq;
using Serilog;
using Serilog.Events;
using Valir.Abstractions;
using Valir.Extensions.Serilog;

namespace Valir.Tests;

/// <summary>
/// Unit tests for LoggingJobHandlerDecorator.
/// Tests the decorator's logging behavior for job execution scenarios.
/// </summary>
public class LoggingJobHandlerDecoratorTests
{
    private readonly Mock<IJobHandler<TestJob>> _innerHandlerMock;
    private readonly Mock<ILogger> _serilogMock;
    private readonly Mock<IJobContextEnricher> _enricherMock;
    private readonly SerilogOptions _options;
    private readonly ValirSerilogLogger _valirLogger;
    private readonly JobContext _jobContext;

    public LoggingJobHandlerDecoratorTests()
    {
        _innerHandlerMock = new Mock<IJobHandler<TestJob>>();
        _serilogMock = new Mock<ILogger>();
        _options = new SerilogOptions();
        _enricherMock = new Mock<IJobContextEnricher>();
        _valirLogger = new ValirSerilogLogger(_serilogMock.Object, _options, _enricherMock.Object);
        _jobContext = new JobContext("test-job-id", "worker-1", "lock-token-123", CancellationToken.None);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullInnerHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LoggingJobHandlerDecorator<TestJob>(null!, _valirLogger, _options));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LoggingJobHandlerDecorator<TestJob>(_innerHandlerMock.Object, null!, _options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LoggingJobHandlerDecorator<TestJob>(_innerHandlerMock.Object, _valirLogger, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Assert
        Assert.NotNull(decorator);
    }

    #endregion

    #region Successful Execution Tests

    [Fact]
    public async Task HandleAsync_SuccessfulExecution_LogsJobStart()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert
        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Information,
            "Job {JobName} started (Attempt {Attempt})",
            It.Is<string>(name => name == typeof(TestJob).FullName),
            It.Is<int>(attempt => attempt == 1)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulExecution_LogsJobComplete()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert
        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Information,
            "Job {JobName} completed in {DurationMs}ms (Attempt {Attempt})",
            It.Is<string>(name => name == typeof(TestJob).FullName),
            It.Is<long>(duration => duration >= 0),
            It.Is<int>(attempt => attempt == 1)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulExecution_CallsInnerHandler()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert
        _innerHandlerMock.Verify(x => x.HandleAsync(job, _jobContext), Times.Once);
    }

    #endregion

    #region Failed Execution Tests

    [Fact]
    public async Task HandleAsync_FailedExecution_LogsJobFailure()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        InvalidOperationException exception = new("Job failed");
        _innerHandlerMock.Setup(x => x.HandleAsync(It.IsAny<TestJob>(), It.IsAny<JobContext>()))
            .ThrowsAsync(exception);
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act & Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => decorator.HandleAsync(job, _jobContext));

        Assert.Equal("Job failed", ex.Message);

        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Error,
            exception,
            "Job {JobName} failed after {DurationMs}ms (Attempt {Attempt})",
            It.Is<string>(name => name == typeof(TestJob).FullName),
            It.Is<long>(duration => duration >= 0),
            It.Is<int>(attempt => attempt == 1)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FailedExecution_RethrowsException()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        InvalidOperationException exception = new("Job failed");
        _innerHandlerMock.Setup(x => x.HandleAsync(It.IsAny<TestJob>(), It.IsAny<JobContext>()))
            .ThrowsAsync(exception);
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act & Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => decorator.HandleAsync(job, _jobContext));

        Assert.Same(exception, ex);
    }

    #endregion

    #region Timing Tests

    [Fact]
    public async Task HandleAsync_WithTimingEnabled_IncludesDurationInCompleteLog()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _options.IncludeTiming = true;
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert
        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Information,
            "Job {JobName} completed in {DurationMs}ms (Attempt {Attempt})",
            It.Is<string>(name => name == typeof(TestJob).FullName),
            It.Is<long>(duration => duration >= 0),
            It.Is<int>(attempt => attempt == 1)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithTimingDisabled_DoesNotIncludeDurationInCompleteLog()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _options.IncludeTiming = false;
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert
        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Information,
            "Job {JobName} completed (Attempt {Attempt})",
            It.Is<string>(name => name == typeof(TestJob).FullName),
            It.Is<int>(attempt => attempt == 1)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithTimingDisabled_DoesNotUseStopwatch()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _options.IncludeTiming = false;
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert - Verify the message without timing is logged
        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Information,
            "Job {JobName} completed (Attempt {Attempt})",
            It.IsAny<string>(),
            It.IsAny<int>()),
            Times.Once);
    }

    #endregion

    #region Log Level Configuration Tests

    [Fact]
    public async Task HandleAsync_WithCustomJobStartLogLevel_UsesConfiguredLevel()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _options.JobStartLogLevel = LogEventLevel.Debug;
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Debug)).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert
        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Debug,
            "Job {JobName} started (Attempt {Attempt})",
            It.IsAny<string>(),
            It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithCustomJobCompleteLogLevel_UsesConfiguredLevel()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _options.JobCompleteLogLevel = LogEventLevel.Debug;
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert
        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Debug,
            "Job {JobName} completed in {DurationMs}ms (Attempt {Attempt})",
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithCustomJobFailureLogLevel_UsesConfiguredLevel()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        InvalidOperationException exception = new("Job failed");
        _innerHandlerMock.Setup(x => x.HandleAsync(It.IsAny<TestJob>(), It.IsAny<JobContext>()))
            .ThrowsAsync(exception);
        _options.JobFailureLogLevel = LogEventLevel.Fatal;
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => decorator.HandleAsync(job, _jobContext));

        _serilogMock.Verify(x => x.Write(
            LogEventLevel.Fatal,
            exception,
            "Job {JobName} failed after {DurationMs}ms (Attempt {Attempt})",
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<int>()),
            Times.Once);
    }

    #endregion

    #region Null Argument Tests

    [Fact]
    public async Task HandleAsync_WithNullJob_ThrowsArgumentNullException()
    {
        // Arrange
        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => decorator.HandleAsync(null!, _jobContext));
    }

    [Fact]
    public async Task HandleAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => decorator.HandleAsync(job, null!));
    }

    #endregion

    #region Payload Logging Tests

    [Fact]
    public async Task HandleAsync_WithPayloadLoggingEnabled_LogsJobPayload()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _options.LogJobPayload = true;
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        _serilogMock.Setup(x => x.IsEnabled(LogEventLevel.Debug)).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert
        _serilogMock.Verify(x => x.Debug(
            "Job {JobName} payload: {Payload}",
            It.Is<string>(name => name == typeof(TestJob).FullName),
            It.Is<string>(payload => payload.Contains("Test"))),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithPayloadLoggingDisabled_DoesNotLogPayload()
    {
        // Arrange
        TestJob job = new() { Id = 1, Name = "Test" };
        _options.LogJobPayload = false;
        _serilogMock.Setup(x => x.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);

        LoggingJobHandlerDecorator<TestJob> decorator = new(
            _innerHandlerMock.Object,
            _valirLogger,
            _options);

        // Act
        await decorator.HandleAsync(job, _jobContext);

        // Assert
        _serilogMock.Verify(x => x.Debug(
            "Job {JobName} payload: {Payload}",
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    /// <summary>
    /// Test job for unit tests.
    /// </summary>
    public class TestJob
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
