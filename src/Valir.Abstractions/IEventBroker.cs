namespace Valir.Abstractions;

/// <summary>
/// Low-level broker adapter contract for Event Bus implementations.
/// </summary>
public interface IEventBroker
{
    /// <summary>
    /// Publish an event envelope to a topic.
    /// </summary>
    Task PublishAsync(string topic, EventEnvelope envelope);

    /// <summary>
    /// Subscribe to a topic with a specific subscription ID for consumer groups.
    /// </summary>
    /// <param name="topic">Topic to subscribe to.</param>
    /// <param name="subscriptionId">Consumer group or subscription identifier.</param>
    /// <param name="onMessage">Handler for received messages.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SubscribeAsync(
        string topic,
        string subscriptionId,
        Func<EventEnvelope, Task> onMessage,
        CancellationToken ct);

    /// <summary>
    /// Unsubscribe from a topic.
    /// </summary>
    Task UnsubscribeAsync(string topic, string subscriptionId);
}
