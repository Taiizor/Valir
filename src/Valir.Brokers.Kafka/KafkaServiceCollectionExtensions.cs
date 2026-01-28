using Microsoft.Extensions.DependencyInjection;
using Valir.Abstractions;

namespace Valir.Brokers.Kafka;

/// <summary>
/// Extension methods for configuring Kafka broker.
/// </summary>
public static class KafkaServiceCollectionExtensions
{
    /// <summary>
    /// Add Kafka as the Event Bus broker.
    /// </summary>
    public static IServiceCollection AddValirKafka(
        this IServiceCollection services,
        Action<KafkaOptions>? configure = null)
    {
        KafkaOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IEventBroker, KafkaEventBroker>();

        return services;
    }
}
