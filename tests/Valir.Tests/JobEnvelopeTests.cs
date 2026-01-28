using Valir.Abstractions;

namespace Valir.Tests;

public class JobEnvelopeTests
{
    [Fact]
    public void JobEnvelope_CreatesWithAllProperties()
    {
        string id = "job-123";
        string type = "send-email";
        byte[] payload = new byte[] { 1, 2, 3 };
        int attempts = 2;
        int maxAttempts = 5;
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        TimeSpan visibilityTimeout = TimeSpan.FromMinutes(5);
        string idempotencyKey = "idempotency-key-123";
        int priority = 10;

        JobEnvelope envelope = new(
            Id: id,
            Type: type,
            Payload: payload,
            Attempts: attempts,
            MaxAttempts: maxAttempts,
            CreatedAt: createdAt,
            VisibilityTimeout: visibilityTimeout,
            IdempotencyKey: idempotencyKey,
            Priority: priority
        );

        Assert.Equal(id, envelope.Id);
        Assert.Equal(type, envelope.Type);
        Assert.Equal(payload, envelope.Payload);
        Assert.Equal(attempts, envelope.Attempts);
        Assert.Equal(maxAttempts, envelope.MaxAttempts);
        Assert.Equal(createdAt, envelope.CreatedAt);
        Assert.Equal(visibilityTimeout, envelope.VisibilityTimeout);
        Assert.Equal(idempotencyKey, envelope.IdempotencyKey);
        Assert.Equal(priority, envelope.Priority);
    }

    [Fact]
    public void JobEnvelope_WithDefaultOptionalProperties()
    {
        JobEnvelope envelope = new(
            Id: "job-1",
            Type: "test",
            Payload: [],
            Attempts: 0,
            MaxAttempts: 3,
            CreatedAt: DateTimeOffset.UtcNow,
            VisibilityTimeout: TimeSpan.FromSeconds(30)
        );

        Assert.Null(envelope.IdempotencyKey);
        Assert.Equal(0, envelope.Priority);
    }

    [Fact]
    public void JobEnvelope_EqualityByValue()
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        byte[] payload = new byte[] { 1, 2, 3 };
        TimeSpan visibilityTimeout = TimeSpan.FromSeconds(30);

        JobEnvelope envelope1 = new("id", "type", payload, 0, 3, createdAt, visibilityTimeout);
        JobEnvelope envelope2 = new("id", "type", payload, 0, 3, createdAt, visibilityTimeout);

        // Records have value equality
        Assert.Equal(envelope1, envelope2);
    }

    [Fact]
    public void JobEnvelope_InequalityOnDifferentId()
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        TimeSpan visibilityTimeout = TimeSpan.FromSeconds(30);

        JobEnvelope envelope1 = new("id-1", "type", [], 0, 3, createdAt, visibilityTimeout);
        JobEnvelope envelope2 = new("id-2", "type", [], 0, 3, createdAt, visibilityTimeout);

        Assert.NotEqual(envelope1, envelope2);
    }

    [Fact]
    public void JobEnvelope_WithPriority()
    {
        JobEnvelope envelope = new(
            Id: "job-1",
            Type: "urgent",
            Payload: [],
            Attempts: 0,
            MaxAttempts: 3,
            CreatedAt: DateTimeOffset.UtcNow,
            VisibilityTimeout: TimeSpan.FromSeconds(30),
            Priority: 100
        );

        Assert.Equal(100, envelope.Priority);
    }

    [Fact]
    public void JobEnvelope_AttemptsIncrement()
    {
        JobEnvelope original = new(
            Id: "job-1",
            Type: "test",
            Payload: [],
            Attempts: 0,
            MaxAttempts: 3,
            CreatedAt: DateTimeOffset.UtcNow,
            VisibilityTimeout: TimeSpan.FromSeconds(30)
        );

        JobEnvelope retried = original with { Attempts = original.Attempts + 1 };

        Assert.Equal(0, original.Attempts);
        Assert.Equal(1, retried.Attempts);
    }
}
