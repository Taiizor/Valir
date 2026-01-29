using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Valir.Abstractions;

namespace Valir.Brokers.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of IEventBroker.
/// </summary>
/// <remarks>
/// Initializes a new instance of the RabbitMQEventBroker.
/// </remarks>
/// <param name="options">Configuration options.</param>
public sealed class RabbitMQEventBroker(RabbitMQOptions options) : IEventBroker, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly Dictionary<string, string> _consumerTags = [];
    private readonly Lock _lock = new();

    private IConnection? _connection;
    private IChannel? _channel;
    private bool _initialized;

    /// <summary>
    /// Ensures connection and channel are initialized.
    /// Uses lazy initialization for testability.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            ConnectionFactory factory = new()
            {
                HostName = options.HostName,
                Port = options.Port,
                VirtualHost = options.VirtualHost,
                UserName = options.UserName,
                Password = options.Password
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            // Declare the exchange
            await _channel.ExchangeDeclareAsync(
                exchange: options.ExchangeName,
                type: options.ExchangeType,
                durable: options.Durable,
                autoDelete: false
            );

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(string topic, EventEnvelope envelope)
    {
        await EnsureInitializedAsync();

        BasicProperties props = new()
        {
            MessageId = envelope.Id,
            Timestamp = new AmqpTimestamp(envelope.PublishedAt.ToUnixTimeSeconds()),
            Persistent = true,
            Headers = new Dictionary<string, object?>
            {
                ["valir.event.id"] = envelope.Id,
                ["valir.event.topic"] = envelope.Topic
            }
        };

        await _channel!.BasicPublishAsync(
            exchange: options.ExchangeName,
            routingKey: topic,
            mandatory: false,
            basicProperties: props,
            body: envelope.Payload
        );
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(
        string topic,
        string subscriptionId,
        Func<EventEnvelope, Task> onMessage,
        CancellationToken ct)
    {
        await EnsureInitializedAsync();

        string queueName = $"valir.{subscriptionId}.{topic}";

        // Declare queue
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: options.Durable,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct
        );

        // Bind to exchange
        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: options.ExchangeName,
            routingKey: topic,
            cancellationToken: ct
        );

        // Set QoS for at-least-once
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: options.PrefetchCount,
            global: false,
            cancellationToken: ct
        );

        AsyncEventingBasicConsumer consumer = new(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                EventEnvelope envelope = new(
                    Id: ea.BasicProperties.MessageId ?? Guid.CreateVersion7().ToString("N"),
                    Topic: topic,
                    Payload: ea.Body.ToArray(),
                    PublishedAt: DateTimeOffset.FromUnixTimeSeconds(ea.BasicProperties.Timestamp.UnixTime)
                );

                await onMessage(envelope);

                // Manual ack for at-least-once
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception)
            {
                // Nack and requeue
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        string consumerTag = await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct
        );

        lock (_lock)
        {
            _consumerTags[$"{topic}:{subscriptionId}"] = consumerTag;
        }

        // Wait for cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, string subscriptionId)
    {
        if (_channel is null)
        {
            return;
        }

        string key = $"{topic}:{subscriptionId}";

        lock (_lock)
        {
            if (_consumerTags.TryGetValue(key, out string? consumerTag))
            {
                _channel.BasicCancelAsync(consumerTag).GetAwaiter().GetResult();
                _consumerTags.Remove(key);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _initLock.Dispose();
    }
}
