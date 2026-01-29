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
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <exception cref="ArgumentException">Thrown when topic is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when payload is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task PublishAsync(string topic, byte[] payload, CancellationToken ct = default);

    /// <summary>
    /// Subscribe to events on a topic.
    /// </summary>
    /// <param name="topic">Topic to subscribe to.</param>
    /// <param name="handler">Async handler for received events.</param>
    /// <param name="ct">Cancellation token to stop subscription.</param>
    /// <returns>A task representing the asynchronous subscribe operation.</returns>
    /// <exception cref="ArgumentException">Thrown when topic is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when handler is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task SubscribeAsync(string topic, Func<EventEnvelope, Task> handler, CancellationToken ct);
}
