using Testcontainers.RabbitMq;
using Valir.Abstractions;
using Valir.Brokers.RabbitMQ;

namespace Valir.Tests.Integration;

/// <summary>
/// Integration tests for RabbitMQ broker using Testcontainers.
/// </summary>
public class RabbitMQIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitContainer;
    private RabbitMQEventBroker _broker = null!;

    public RabbitMQIntegrationTests()
    {
        _rabbitContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _rabbitContainer.StartAsync();

        _broker = new RabbitMQEventBroker(new RabbitMQOptions
        {
            HostName = _rabbitContainer.Hostname,
            Port = _rabbitContainer.GetMappedPublicPort(5672),
            UserName = "testuser",
            Password = "testpass"
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _broker.DisposeAsync();
        await _rabbitContainer.DisposeAsync();
    }

    [Fact]
    public async Task PublishAsync_ShouldPublishEventSuccessfully()
    {
        // Arrange
        EventEnvelope envelope = new(
            Id: Guid.CreateVersion7().ToString(),
            Topic: "test-topic",
            Payload: System.Text.Encoding.UTF8.GetBytes("""{"message":"hello from rabbit"}"""),
            PublishedAt: DateTimeOffset.UtcNow
        );

        // Act
        await _broker.PublishAsync("test-topic", envelope);

        // Assert - if no exception, publish succeeded
        Assert.True(true);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldReceivePublishedEvent()
    {
        // Arrange
        string topic = $"test-topic-{Guid.CreateVersion7():N}";
        TaskCompletionSource<EventEnvelope> receivedEnvelope = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

        EventEnvelope envelope = new(
            Id: Guid.CreateVersion7().ToString(),
            Topic: topic,
            Payload: System.Text.Encoding.UTF8.GetBytes("""{"test":"rabbit-data"}"""),
            PublishedAt: DateTimeOffset.UtcNow
        );

        // Act - Start subscription in background
        _ = Task.Run(async () =>
        {
            await _broker.SubscribeAsync(
                topic,
                $"test-subscriber-{Guid.CreateVersion7():N}",
                e =>
                {
                    receivedEnvelope.TrySetResult(e);
                    return Task.CompletedTask;
                },
                cts.Token
            );
        });

        // Give subscriber time to start and create queue
        await Task.Delay(2000);

        await _broker.PublishAsync(topic, envelope);

        // Assert
        EventEnvelope received = await receivedEnvelope.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(envelope.Id, received.Id);
        Assert.Equal(envelope.Topic, received.Topic);
    }
}
