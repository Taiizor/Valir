using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Valir.Abstractions;
using Valir.Core;
using Valir.Redis;

namespace Valir.AspNet;

/// <summary>
/// DI extension methods for Valir integration.
/// </summary>
public static class ValirServiceCollectionExtensions
{
    /// <summary>
    /// Add Valir services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValir(
        this IServiceCollection services,
        Action<ValirOptions>? configure = null)
    {
        ValirOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            ConfigurationOptions configOptions = ParseRedisConnectionString(options.RedisConnectionString);
            ApplyValirRedisDefaults(configOptions);
            return ConnectionMultiplexer.Connect(configOptions);
        });
        services.AddSingleton<IJobQueue, RedisJobQueue>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        services.AddSingleton<IValirMetrics, ValirMetricsAdapter>();

        if (options.AutoRegisterHealthChecks)
        {
            services.AddHealthChecks()
                .AddValirHealthChecks();
        }

        return services;
    }

    /// <summary>
    /// Add Valir health checks to the DI container.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="tags">Optional tags for the health checks.</param>
    /// <returns>The health checks builder for chaining.</returns>
    public static IHealthChecksBuilder AddValirHealthChecks(
        this IHealthChecksBuilder builder,
        string[]? tags = null)
    {
        tags ??= ["valir", "redis", "queue"];

        builder.AddCheck<RedisHealthCheck>(
            "valir-redis",
            tags: tags);

        builder.AddCheck<JobQueueHealthCheck>(
            "valir-queue",
            tags: tags);

        builder.AddCheck<ValirHealthCheck>(
            "valir",
            tags: tags);

        return builder;
    }

    /// <summary>
    /// Add Valir job queue only (for producer applications).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="redisConnectionString">Redis connection string.</param>
    /// <param name="configureOptions">Optional Redis configuration options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValirQueue(
        this IServiceCollection services,
        string redisConnectionString,
        Action<ConfigurationOptions>? configureOptions = null)
    {
        ValirOptions options = new() { RedisConnectionString = redisConnectionString };
        services.AddSingleton(options);
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            ConfigurationOptions configOptions = ParseRedisConnectionString(redisConnectionString);
            ApplyValirRedisDefaults(configOptions);
            configureOptions?.Invoke(configOptions);
            return ConnectionMultiplexer.Connect(configOptions);
        });
        services.AddSingleton<IJobQueue, RedisJobQueue>();

        return services;
    }

    /// <summary>
    /// Add Valir with custom Redis configuration options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Redis configuration options.</param>
    /// <param name="configureValir">Optional Valir configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValirWithRedisOptions(
        this IServiceCollection services,
        Action<ConfigurationOptions> configureOptions,
        Action<ValirOptions>? configureValir = null)
    {
        ValirOptions options = new();
        configureValir?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            ConfigurationOptions configOptions = new();
            ApplyValirRedisDefaults(configOptions);
            configureOptions(configOptions);
            return ConnectionMultiplexer.Connect(configOptions);
        });
        services.AddSingleton<IJobQueue, RedisJobQueue>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();

        return services;
    }

    /// <summary>
    /// Parses a Redis connection string into ConfigurationOptions.
    /// </summary>
    private static ConfigurationOptions ParseRedisConnectionString(string connectionString)
    {
        // Check if it's already a configuration string with options
        if (connectionString.Contains(','))
        {
            return ConfigurationOptions.Parse(connectionString);
        }

        // Simple host:port format
        string[] parts = connectionString.Split(':');
        string host = parts[0];
        int port = parts.Length > 1 && int.TryParse(parts[1], out int p) ? p : 6379;

        return new ConfigurationOptions
        {
            EndPoints = { { host, port } }
        };
    }

    /// <summary>
    /// Applies Valir-specific defaults to Redis configuration.
    /// </summary>
    private static void ApplyValirRedisDefaults(ConfigurationOptions options)
    {
        // Connection resilience
        options.ConnectRetry = 3;
        options.ConnectTimeout = 5000; // 5 seconds
        options.SyncTimeout = 5000; // 5 seconds
        options.AsyncTimeout = 5000; // 5 seconds

        // Reconnection settings
        options.ReconnectRetryPolicy = new ExponentialRetry(5000);
        options.KeepAlive = 60; // Keep connections alive

        // Pool settings for high-throughput scenarios
        options.DefaultDatabase = 0;

        // Abort on connect fail - set to false for resilience
        options.AbortOnConnectFail = false;

        // Enable performance counters for monitoring (optional)
        // options.IncludePerformanceCountersInExceptions = true;
    }
}
