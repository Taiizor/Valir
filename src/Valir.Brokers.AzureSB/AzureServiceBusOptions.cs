namespace Valir.Brokers.AzureSB;

/// <summary>
/// Configuration options for Azure Service Bus broker.
/// </summary>
public class AzureServiceBusOptions
{
    /// <summary>
    /// Azure Service Bus connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use sessions for ordered message delivery.
    /// </summary>
    public bool EnableSessions { get; set; }

    /// <summary>
    /// Maximum number of concurrent calls to the callback.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 10;

    /// <summary>
    /// Prefetch count for performance optimization.
    /// </summary>
    public int PrefetchCount { get; set; } = 20;

    /// <summary>
    /// Maximum auto lock renewal duration.
    /// </summary>
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);
}
