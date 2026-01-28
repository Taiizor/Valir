namespace Valir.Abstractions;

/// <summary>
/// Represents an event envelope for the Event Bus.
/// </summary>
/// <param name="Id">Unique identifier for this event.</param>
/// <param name="Topic">The topic/channel this event was published to.</param>
/// <param name="Payload">Serialized event payload as bytes.</param>
/// <param name="PublishedAt">Timestamp when the event was published.</param>
public sealed record EventEnvelope(
    string Id,
    string Topic,
    byte[] Payload,
    DateTimeOffset PublishedAt
);
