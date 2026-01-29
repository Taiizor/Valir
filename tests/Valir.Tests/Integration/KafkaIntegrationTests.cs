using System.Text;
using Testcontainers.Kafka;
using Valir.Abstractions;
using Valir.Brokers.Kafka;

namespace Valir.Tests.Integration;

/// <summary>
/// Integration tests for Kafka broker using Testcontainers.
/// </summary>
public class KafkaIntegrationTests : IAsyncLifetime
{
    private readonly KafkaContainer _kafkaContainer;
    private KafkaEventBroker _broker = null!;

    public KafkaIntegrationTests()
    {
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.6.0")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _kafkaContainer.StartAsync();

        _broker = new KafkaEventBroker(new KafkaOptions
        {
            BootstrapServers = _kafkaContainer.GetBootstrapAddress()
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _broker.DisposeAsync();
        await _kafkaContainer.DisposeAsync();
    }

    [Fact]
    public async Task PublishAsync_ShouldPublishEventSuccessfully()
    {
        // Arrange
        EventEnvelope envelope = new(
            Id: Guid.CreateVersion7().ToString(),
            Topic: "test-topic",
            Payload: Encoding.UTF8.GetBytes("""{"message":"hello"}"""),
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
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));

        EventEnvelope envelope = new(
            Id: Guid.CreateVersion7().ToString(),
            Topic: topic,
            Payload: Encoding.UTF8.GetBytes("""{"test":"kafka-data"}"""),
            PublishedAt: DateTimeOffset.UtcNow
        );

        // First publish to create topic (Kafka auto-creates topics on first produce)
        await _broker.PublishAsync(topic, envelope);

        // Wait for topic metadata propagation
        await Task.Delay(3000, TestContext.Current.CancellationToken);

        // Act - Start subscription in background
        _ = Task.Run(async () =>
        {
            await _broker.SubscribeAsync(
                topic,
                $"test-group-{Guid.CreateVersion7():N}",
                e =>
                {
                    receivedEnvelope.TrySetResult(e);
                    return Task.CompletedTask;
                },
                cts.Token
            );
        }, TestContext.Current.CancellationToken);

        // Give consumer time to join group and rebalance
        await Task.Delay(5000, TestContext.Current.CancellationToken);

        // Publish again after consumer is ready
        EventEnvelope secondEnvelope = new(
            Id: Guid.CreateVersion7().ToString(),
            Topic: topic,
            Payload: Encoding.UTF8.GetBytes("""{"test":"kafka-data-2"}"""),
            PublishedAt: DateTimeOffset.UtcNow
        );
        await _broker.PublishAsync(topic, secondEnvelope);

        // Assert
        EventEnvelope received = await receivedEnvelope.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.NotNull(received);
        Assert.Equal(topic, received.Topic);
    }
}
