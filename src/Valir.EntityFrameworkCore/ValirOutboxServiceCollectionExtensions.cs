using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Valir.EntityFrameworkCore;

/// <summary>
/// Extension methods for configuring Valir outbox services.
/// </summary>
public static class ValirOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Add the Valir outbox processor background service.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type containing the outbox table.</typeparam>
    public static IServiceCollection AddValirOutbox<TContext>(
        this IServiceCollection services,
        Action<OutboxProcessorOptions>? configure = null)
        where TContext : DbContext
    {
        OutboxProcessorOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHostedService<OutboxProcessor<TContext>>();

        return services;
    }
}
