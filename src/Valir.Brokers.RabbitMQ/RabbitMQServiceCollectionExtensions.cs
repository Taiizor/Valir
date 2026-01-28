using Microsoft.Extensions.DependencyInjection;
using Valir.Abstractions;

namespace Valir.Brokers.RabbitMQ;

/// <summary>
/// Extension methods for configuring RabbitMQ broker.
/// </summary>
public static class RabbitMQServiceCollectionExtensions
{
    /// <summary>
    /// Add RabbitMQ as the Event Bus broker.
    /// </summary>
    public static IServiceCollection AddValirRabbitMQ(
        this IServiceCollection services,
        Action<RabbitMQOptions>? configure = null)
    {
        RabbitMQOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IEventBroker, RabbitMQEventBroker>();

        return services;
    }
}
