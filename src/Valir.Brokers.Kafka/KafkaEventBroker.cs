using Confluent.Kafka;
using System.Text;
using Valir.Abstractions;

namespace Valir.Brokers.Kafka;

/// <summary>
/// Kafka implementation of IEventBroker.
/// </summary>
public sealed class KafkaEventBroker : IEventBroker, IAsyncDisposable
{
    private readonly KafkaOptions _options;
    private readonly IProducer<string, byte[]> _producer;
    private readonly Dictionary<string, IConsumer<string, byte[]>> _consumers = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the KafkaEventBroker.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    public KafkaEventBroker(KafkaOptions options)
    {
        _options = options;

        ProducerConfig producerConfig = new()
        {
            BootstrapServers = options.BootstrapServers,
            MessageTimeoutMs = options.MessageTimeoutMs
        };

        ApplySecurityConfig(producerConfig);

        _producer = new ProducerBuilder<string, byte[]>(producerConfig).Build();
    }

    /// <inheritdoc />
    public async Task PublishAsync(string topic, EventEnvelope envelope)
    {
        Message<string, byte[]> message = new()
        {
            Key = envelope.Id,
            Value = envelope.Payload,
            Headers = new Headers
            {
                { "valir.event.id", Encoding.UTF8.GetBytes(envelope.Id) },
                { "valir.event.topic", Encoding.UTF8.GetBytes(envelope.Topic) },
                { "valir.published.at", Encoding.UTF8.GetBytes(envelope.PublishedAt.ToString("O")) }
            }
        };

        await _producer.ProduceAsync(topic, message);
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(
        string topic,
        string subscriptionId,
        Func<EventEnvelope, Task> onMessage,
        CancellationToken ct)
    {
        ConsumerConfig consumerConfig = new()
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = $"{_options.GroupId}-{subscriptionId}",
            EnableAutoCommit = _options.EnableAutoCommit,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_options.AutoOffsetReset, true)
        };

        ApplySecurityConfig(consumerConfig);

        IConsumer<string, byte[]> consumer = new ConsumerBuilder<string, byte[]>(consumerConfig).Build();

        lock (_lock)
        {
            _consumers[$"{topic}:{subscriptionId}"] = consumer;
        }

        consumer.Subscribe(topic);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, byte[]> result = consumer.Consume(ct);

                    if (result?.Message is null)
                    {
                        continue;
                    }

                    EventEnvelope envelope = new(
                        Id: result.Message.Key ?? Guid.CreateVersion7().ToString("N"),
                        Topic: topic,
                        Payload: result.Message.Value,
                        PublishedAt: result.Message.Timestamp.UtcDateTime
                    );

                    await onMessage(envelope);

                    if (!_options.EnableAutoCommit)
                    {
                        consumer.Commit(result);
                    }
                }
                catch (ConsumeException ex)
                {
                    // Log and continue
                    Console.Error.WriteLine($"Kafka consume error: {ex.Message}");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(string topic, string subscriptionId)
    {
        string key = $"{topic}:{subscriptionId}";

        lock (_lock)
        {
            if (_consumers.TryGetValue(key, out IConsumer<string, byte[]>? consumer))
            {
                consumer.Unsubscribe();
                consumer.Close();
                _consumers.Remove(key);
            }
        }

        return Task.CompletedTask;
    }

    private void ApplySecurityConfig(ClientConfig config)
    {
        if (!string.IsNullOrEmpty(_options.SecurityProtocol))
        {
            config.SecurityProtocol = Enum.Parse<SecurityProtocol>(_options.SecurityProtocol, true);
        }

        if (!string.IsNullOrEmpty(_options.SaslMechanism))
        {
            config.SaslMechanism = Enum.Parse<SaslMechanism>(_options.SaslMechanism, true);
            config.SaslUsername = _options.SaslUsername;
            config.SaslPassword = _options.SaslPassword;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _producer.Dispose();

        lock (_lock)
        {
            foreach (IConsumer<string, byte[]> consumer in _consumers.Values)
            {
                consumer.Close();
                consumer.Dispose();
            }
            _consumers.Clear();
        }
    }
}
