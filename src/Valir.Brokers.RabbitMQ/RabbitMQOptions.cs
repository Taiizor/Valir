namespace Valir.Brokers.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ broker.
/// </summary>
public class RabbitMQOptions
{
    /// <summary>
    /// RabbitMQ host name.
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ port.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Virtual host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Exchange name for publishing.
    /// </summary>
    public string ExchangeName { get; set; } = "valir.events";

    /// <summary>
    /// Exchange type (direct, fanout, topic, headers).
    /// </summary>
    public string ExchangeType { get; set; } = "topic";

    /// <summary>
    /// Whether to use durable exchanges/queues.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Consumer prefetch count for at-least-once delivery.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;
}
