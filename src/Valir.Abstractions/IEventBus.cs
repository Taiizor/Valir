namespace Valir.Abstractions;

/// <summary>
/// Interface for publishing and subscribing to events.
/// Broker-agnostic with adapters for Kafka, RabbitMQ, Azure Service Bus.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publish an event to a topic.
    /// </summary>
    /// <param name="topic">Target topic/channel.</param>
    /// <param name="payload">Serialized event payload.</param>
    Task PublishAsync(string topic, byte[] payload);

    /// <summary>
    /// Subscribe to events on a topic.
    /// </summary>
    /// <param name="topic">Topic to subscribe to.</param>
    /// <param name="handler">Async handler for received events.</param>
    /// <param name="ct">Cancellation token to stop subscription.</param>
    Task SubscribeAsync(string topic, Func<EventEnvelope, Task> handler, CancellationToken ct);
}
