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
    public static IServiceCollection AddValir(
        this IServiceCollection services,
        Action<ValirOptions>? configure = null)
    {
        ValirOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(options.RedisConnectionString));
        services.AddSingleton<IJobQueue, RedisJobQueue>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();

        return services;
    }

    /// <summary>
    /// Add Valir job queue only (for producer applications).
    /// </summary>
    public static IServiceCollection AddValirQueue(
        this IServiceCollection services,
        string redisConnectionString)
    {
        ValirOptions options = new() { RedisConnectionString = redisConnectionString };
        services.AddSingleton(options);
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton<IJobQueue, RedisJobQueue>();

        return services;
    }
}
