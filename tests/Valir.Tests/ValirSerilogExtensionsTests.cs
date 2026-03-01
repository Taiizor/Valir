using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Valir.Abstractions;
using Valir.Extensions.Serilog;

namespace Valir.Tests;

/// <summary>
/// Unit tests for ValirSerilogExtensions.
/// Tests the DI extension methods for registering Serilog services.
/// </summary>
public class ValirSerilogExtensionsTests
{
    #region AddValirSerilog - No Parameters

    [Fact]
    public void AddValirSerilog_WithNoParameters_RegistersSerilogOptions()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        SerilogOptions? options = provider.GetService<SerilogOptions>();
        Assert.NotNull(options);
    }

    [Fact]
    public void AddValirSerilog_WithNoParameters_RegistersJobContextEnricher()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        JobContextEnricher? enricher = provider.GetService<JobContextEnricher>();
        Assert.NotNull(enricher);
    }

    [Fact]
    public void AddValirSerilog_WithNoParameters_RegistersValirSerilogLogger()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        ValirSerilogLogger? logger = provider.GetService<ValirSerilogLogger>();
        Assert.NotNull(logger);
    }

    [Fact]
    public void AddValirSerilog_WithNoParameters_RegistersJobContextEnricherAsSelf()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        JobContextEnricher? enricher = provider.GetService<JobContextEnricher>();
        Assert.NotNull(enricher);
    }

    [Fact]
    public void AddValirSerilog_WithNoParameters_ReturnsServiceCollection()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection result = services.AddValirSerilog();

        // Assert
        Assert.Same(services, result);
    }

    #endregion

    #region AddValirSerilog - With Options Configuration

    [Fact]
    public void AddValirSerilog_WithOptionsConfiguration_AppliesConfiguration()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog(options =>
        {
            options.JobIdPropertyName = "CustomJobId";
            options.IncludeTiming = false;
            options.LogJobPayload = true;
        });
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        SerilogOptions options = provider.GetRequiredService<SerilogOptions>();
        Assert.Equal("CustomJobId", options.JobIdPropertyName);
        Assert.False(options.IncludeTiming);
        Assert.True(options.LogJobPayload);
    }

    [Fact]
    public void AddValirSerilog_WithOptionsConfiguration_RegistersAllServices()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog(options =>
        {
            options.MinimumLogLevel = Serilog.Events.LogEventLevel.Debug;
        });
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<SerilogOptions>());
        Assert.NotNull(provider.GetService<JobContextEnricher>());
        Assert.NotNull(provider.GetService<ValirSerilogLogger>());
    }

    [Fact]
    public void AddValirSerilog_WithNullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services!.AddValirSerilog(_ => { }));
    }

    [Fact]
    public void AddValirSerilog_WithNullConfigureOptions_ThrowsArgumentNullException()
    {
        // Arrange
        ServiceCollection services = new();
        Action<SerilogOptions>? configureOptions = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddValirSerilog(configureOptions!));
    }

    #endregion

    #region AddValirSerilog - With Custom Logger

    [Fact]
    public void AddValirSerilog_WithCustomLogger_RegistersServices()
    {
        // Arrange
        ServiceCollection services = new();
        Logger logger = new LoggerConfiguration().CreateLogger();

        // Act
        services.AddValirSerilog(logger);
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<SerilogOptions>());
        Assert.NotNull(provider.GetService<JobContextEnricher>());
        Assert.NotNull(provider.GetService<ValirSerilogLogger>());
    }

    [Fact]
    public void AddValirSerilog_WithCustomLoggerAndOptions_AppliesConfiguration()
    {
        // Arrange
        ServiceCollection services = new();
        Logger logger = new LoggerConfiguration().CreateLogger();

        // Act
        services.AddValirSerilog(logger, options =>
        {
            options.WorkerIdPropertyName = "CustomWorkerId";
            options.EnrichWithJobContext = false;
        });
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        SerilogOptions options = provider.GetRequiredService<SerilogOptions>();
        Assert.Equal("CustomWorkerId", options.WorkerIdPropertyName);
        Assert.False(options.EnrichWithJobContext);
    }

    [Fact]
    public void AddValirSerilog_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        ServiceCollection services = new();
        ILogger? logger = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddValirSerilog(logger!));
    }

    [Fact]
    public void AddValirSerilog_WithNullServicesAndLogger_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;
        Logger logger = new LoggerConfiguration().CreateLogger();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services!.AddValirSerilog(logger));
    }

    #endregion

    #region Service Lifetime Tests

    [Fact]
    public void AddValirSerilog_SerilogOptions_IsSingleton()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        SerilogOptions? options1 = provider.GetService<SerilogOptions>();
        SerilogOptions? options2 = provider.GetService<SerilogOptions>();
        Assert.Same(options1, options2);
    }

    [Fact]
    public void AddValirSerilog_JobContextEnricher_IsSingleton()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        JobContextEnricher? enricher1 = provider.GetService<JobContextEnricher>();
        JobContextEnricher? enricher2 = provider.GetService<JobContextEnricher>();
        Assert.Same(enricher1, enricher2);
    }

    [Fact]
    public void AddValirSerilog_ValiSerilogLogger_IsSingleton()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        ValirSerilogLogger? logger1 = provider.GetService<ValirSerilogLogger>();
        ValirSerilogLogger? logger2 = provider.GetService<ValirSerilogLogger>();
        Assert.Same(logger1, logger2);
    }

    #endregion

    #region Idempotent Registration Tests

    [Fact]
    public void AddValirSerilog_CalledMultipleTimes_RegistersOnlyOnce()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog();
        services.AddValirSerilog();
        services.AddValirSerilog();

        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        List<JobContextEnricher> enrichers = [.. provider.GetServices<JobContextEnricher>()];
        Assert.Single(enrichers);
    }

    [Fact]
    public void AddValirSerilog_CalledWithDifferentConfigurations_FirstConfigurationWins()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog(options =>
        {
            options.JobIdPropertyName = "FirstId";
        });
        services.AddValirSerilog(options =>
        {
            options.JobIdPropertyName = "SecondId";
        });

        ServiceProvider provider = services.BuildServiceProvider();
        SerilogOptions options = provider.GetRequiredService<SerilogOptions>();

        // Assert - First configuration wins (TryAddSingleton behavior)
        Assert.Equal("FirstId", options.JobIdPropertyName);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void AddValirSerilog_ValirSerilogLogger_ResolvesAllDependencies()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog(options =>
        {
            options.IncludeTiming = true;
            options.LogJobPayload = true;
        });
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        ValirSerilogLogger logger = provider.GetRequiredService<ValirSerilogLogger>();
        Assert.NotNull(logger);

        // Verify we can create a ForContext (uses enricher internally)
        JobContext context = new("job-1", "worker-1", "token-1", CancellationToken.None);
        ILogger contextualLogger = logger.ForContext(context, "TestJob", 1);
        Assert.NotNull(contextualLogger);
    }

    [Fact]
    public void AddValirSerilog_JobContextEnricher_ResolvedAsSelf()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddValirSerilog();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        JobContextEnricher? enricher = provider.GetService<JobContextEnricher>();
        Assert.NotNull(enricher);
    }

    #endregion
}
