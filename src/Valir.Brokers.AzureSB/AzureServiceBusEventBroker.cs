using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Valir.Abstractions;

namespace Valir.Brokers.AzureSB;

/// <summary>
/// Azure Service Bus implementation of IEventBroker.
/// </summary>
/// <remarks>
/// Initializes a new instance of the AzureServiceBusEventBroker.
/// </remarks>
/// <param name="options">Configuration options.</param>
/// <param name="logger">Logger instance.</param>
public sealed class AzureServiceBusEventBroker(AzureServiceBusOptions options, ILogger<AzureServiceBusEventBroker> logger) : IEventBroker, IAsyncDisposable
{
    private readonly ServiceBusClient _client = new(options.ConnectionString);
    private readonly Dictionary<string, ServiceBusSender> _senders = [];
    private readonly Dictionary<string, ServiceBusProcessor> _processors = [];
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public async Task PublishAsync(string topic, EventEnvelope envelope)
    {
        ServiceBusSender sender = GetOrCreateSender(topic);

        ServiceBusMessage message = new(envelope.Payload)
        {
            MessageId = envelope.Id,
            Subject = envelope.Topic,
            ApplicationProperties =
            {
                ["valir.event.id"] = envelope.Id,
                ["valir.event.topic"] = envelope.Topic,
                ["valir.event.publishedAt"] = envelope.PublishedAt.ToUnixTimeMilliseconds()
            }
        };

        await sender.SendMessageAsync(message);
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(
        string topic,
        string subscriptionId,
        Func<EventEnvelope, Task> onMessage,
        CancellationToken ct)
    {
        // Create processor for the subscription
        ServiceBusProcessor processor = _client.CreateProcessor(
            topic,
            subscriptionId,
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = options.MaxConcurrentCalls,
                AutoCompleteMessages = false,
                PrefetchCount = options.PrefetchCount,
                MaxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration
            });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                EventEnvelope envelope = new(
                    Id: args.Message.MessageId ?? Guid.CreateVersion7().ToString("N"),
                    Topic: topic,
                    Payload: args.Message.Body.ToArray(),
                    PublishedAt: args.Message.EnqueuedTime
                );

                await onMessage(envelope);

                // Manual complete for at-least-once
                await args.CompleteMessageAsync(args.Message, ct);
            }
            catch (Exception)
            {
                // Abandon to retry
                await args.AbandonMessageAsync(args.Message, cancellationToken: ct);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus error");
            return Task.CompletedTask;
        };

        lock (_lock)
        {
            _processors[$"{topic}:{subscriptionId}"] = processor;
        }

        await processor.StartProcessingAsync(ct);

        // Wait for cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            await processor.StopProcessingAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, string subscriptionId)
    {
        string key = $"{topic}:{subscriptionId}";

        lock (_lock)
        {
            if (_processors.TryGetValue(key, out ServiceBusProcessor? processor))
            {
                processor.StopProcessingAsync().GetAwaiter().GetResult();
                processor.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _processors.Remove(key);
            }
        }
    }

    private ServiceBusSender GetOrCreateSender(string topic)
    {
        lock (_lock)
        {
            if (!_senders.TryGetValue(topic, out ServiceBusSender? sender))
            {
                sender = _client.CreateSender(topic);
                _senders[topic] = sender;
            }
            return sender;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            foreach (ServiceBusSender sender in _senders.Values)
            {
                sender.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            _senders.Clear();

            foreach (ServiceBusProcessor processor in _processors.Values)
            {
                processor.StopProcessingAsync().GetAwaiter().GetResult();
                processor.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            _processors.Clear();
        }

        await _client.DisposeAsync();
    }
}
