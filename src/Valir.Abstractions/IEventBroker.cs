namespace Valir.Abstractions;

/// <summary>
/// Low-level broker adapter contract for Event Bus implementations.
/// </summary>
public interface IEventBroker
{
    /// <summary>
    /// Publish an event envelope to a topic.
    /// </summary>
    /// <param name="topic">Target topic/channel.</param>
    /// <param name="envelope">Event envelope containing payload and metadata.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <exception cref="ArgumentException">Thrown when topic is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when envelope is null.</exception>
    Task PublishAsync(string topic, EventEnvelope envelope);

    /// <summary>
    /// Subscribe to a topic with a specific subscription ID for consumer groups.
    /// </summary>
    /// <param name="topic">Topic to subscribe to.</param>
    /// <param name="subscriptionId">Consumer group or subscription identifier.</param>
    /// <param name="onMessage">Handler for received messages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous subscribe operation.</returns>
    /// <exception cref="ArgumentException">Thrown when topic or subscriptionId is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when onMessage is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task SubscribeAsync(
        string topic,
        string subscriptionId,
        Func<EventEnvelope, Task> onMessage,
        CancellationToken ct);

    /// <summary>
    /// Unsubscribe from a topic.
    /// </summary>
    /// <param name="topic">Topic to unsubscribe from.</param>
    /// <param name="subscriptionId">Consumer group or subscription identifier.</param>
    /// <returns>A task representing the asynchronous unsubscribe operation.</returns>
    /// <exception cref="ArgumentException">Thrown when topic or subscriptionId is null or empty.</exception>
    Task UnsubscribeAsync(string topic, string subscriptionId);
}
