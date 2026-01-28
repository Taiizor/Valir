using Microsoft.Extensions.DependencyInjection;
using Valir.Abstractions;

namespace Valir.Brokers.AzureSB;

/// <summary>
/// Extension methods for configuring Azure Service Bus broker.
/// </summary>
public static class AzureServiceBusServiceCollectionExtensions
{
    /// <summary>
    /// Add Azure Service Bus as the Event Bus broker.
    /// </summary>
    public static IServiceCollection AddValirAzureServiceBus(
        this IServiceCollection services,
        Action<AzureServiceBusOptions>? configure = null)
    {
        AzureServiceBusOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IEventBroker, AzureServiceBusEventBroker>();

        return services;
    }
}
