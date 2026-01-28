namespace Valir.Brokers.Kafka;

/// <summary>
/// Configuration options for Kafka broker.
/// </summary>
public class KafkaOptions
{
    /// <summary>
    /// Kafka bootstrap servers (comma-separated).
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Consumer group ID for subscriptions.
    /// </summary>
    public string GroupId { get; set; } = "valir-consumer";

    /// <summary>
    /// Enable auto commit (set false for at-least-once delivery).
    /// </summary>
    public bool EnableAutoCommit { get; set; } = false;

    /// <summary>
    /// Auto offset reset behavior.
    /// </summary>
    public string AutoOffsetReset { get; set; } = "earliest";

    /// <summary>
    /// Message timeout for producer.
    /// </summary>
    public int MessageTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Security protocol (Plaintext, Ssl, SaslPlaintext, SaslSsl).
    /// </summary>
    public string? SecurityProtocol { get; set; }

    /// <summary>
    /// SASL mechanism for authentication.
    /// </summary>
    public string? SaslMechanism { get; set; }

    /// <summary>
    /// SASL username.
    /// </summary>
    public string? SaslUsername { get; set; }

    /// <summary>
    /// SASL password.
    /// </summary>
    public string? SaslPassword { get; set; }
}
