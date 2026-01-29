using Valir.Abstractions;

namespace Valir.Tests;

public class EventEnvelopeTests
{
    [Fact]
    public void EventEnvelope_CreatesWithAllProperties()
    {
        string id = "event-123";
        string topic = "orders.created";
        byte[] payload = [4, 5, 6];
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow;

        EventEnvelope envelope = new(
            Id: id,
            Topic: topic,
            Payload: payload,
            PublishedAt: publishedAt
        );

        Assert.Equal(id, envelope.Id);
        Assert.Equal(topic, envelope.Topic);
        Assert.Equal(payload, envelope.Payload);
        Assert.Equal(publishedAt, envelope.PublishedAt);
    }

    [Fact]
    public void EventEnvelope_EmptyPayload()
    {
        EventEnvelope envelope = new(
            Id: "id",
            Topic: "topic",
            Payload: [],
            PublishedAt: DateTimeOffset.UtcNow
        );

        Assert.Empty(envelope.Payload);
    }

    [Fact]
    public void EventEnvelope_EqualityByValue()
    {
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow;
        byte[] payload = [1, 2, 3];

        EventEnvelope envelope1 = new("id", "topic", payload, publishedAt);
        EventEnvelope envelope2 = new("id", "topic", payload, publishedAt);

        Assert.Equal(envelope1, envelope2);
    }

    [Fact]
    public void EventEnvelope_InequalityOnDifferentTopic()
    {
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow;

        EventEnvelope envelope1 = new("id", "topic-1", [], publishedAt);
        EventEnvelope envelope2 = new("id", "topic-2", [], publishedAt);

        Assert.NotEqual(envelope1, envelope2);
    }
}
