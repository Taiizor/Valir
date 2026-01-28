# Event Bus

Valir supports multiple message brokers for event-driven architectures.

## Overview

The `IEventBroker` interface provides a unified API for publishing and subscribing to events:

```csharp
public interface IEventBroker
{
    Task PublishAsync(string topic, EventEnvelope envelope);
    Task SubscribeAsync(string topic, string subscriptionId, 
        Func<EventEnvelope, Task> onMessage, CancellationToken ct);
    Task UnsubscribeAsync(string topic, string subscriptionId);
}
```

## Kafka

### Installation

```bash
dotnet add package Valir.Brokers.Kafka
```

### Configuration

```csharp
builder.Services.AddValirKafka(options =>
{
    options.BootstrapServers = "localhost:9092";
    options.GroupId = "my-service";
    options.EnableAutoCommit = false;  // At-least-once delivery
    options.AutoOffsetReset = "earliest";
});
```

### Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BootstrapServers` | `string` | `localhost:9092` | Kafka broker addresses |
| `GroupId` | `string` | required | Consumer group ID |
| `EnableAutoCommit` | `bool` | `false` | Auto-commit offsets |
| `AutoOffsetReset` | `string` | `earliest` | Where to start reading |
| `SecurityProtocol` | `string` | `Plaintext` | SASL security |
| `SaslMechanism` | `string` | - | SASL mechanism |
| `SaslUsername` | `string` | - | SASL username |
| `SaslPassword` | `string` | - | SASL password |

### Usage

```csharp
app.MapPost("/events", async (IEventBroker broker, OrderCreatedEvent evt) =>
{
    var envelope = new EventEnvelope(
        Id: Guid.NewGuid().ToString("N"),
        Topic: "orders.created",
        Payload: JsonSerializer.SerializeToUtf8Bytes(evt),
        PublishedAt: DateTimeOffset.UtcNow
    );
    
    await broker.PublishAsync("orders.created", envelope);
    return Results.Accepted();
});
```

## RabbitMQ

### Installation

```bash
dotnet add package Valir.Brokers.RabbitMQ
```

### Configuration

```csharp
builder.Services.AddValirRabbitMQ(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.VirtualHost = "/";
    options.ExchangeName = "valir.events";
    options.ExchangeType = "topic";
});
```

### Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HostName` | `string` | `localhost` | RabbitMQ host |
| `Port` | `int` | `5672` | RabbitMQ port |
| `UserName` | `string` | `guest` | Username |
| `Password` | `string` | `guest` | Password |
| `VirtualHost` | `string` | `/` | Virtual host |
| `ExchangeName` | `string` | `valir.events` | Exchange name |
| `ExchangeType` | `string` | `topic` | Exchange type |
| `Durable` | `bool` | `true` | Durable queues |
| `PrefetchCount` | `ushort` | `10` | Prefetch count |

### Subscribing

```csharp
public class OrderEventHandler : BackgroundService
{
    private readonly IEventBroker _broker;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _broker.SubscribeAsync(
            topic: "orders.*",
            subscriptionId: "order-handler",
            onMessage: async envelope =>
            {
                var order = JsonSerializer.Deserialize<OrderEvent>(envelope.Payload);
                await ProcessOrderEventAsync(order);
            },
            ct: ct
        );
    }
}
```

## Azure Service Bus

### Installation

```bash
dotnet add package Valir.Brokers.AzureSB
```

### Configuration

```csharp
builder.Services.AddValirAzureServiceBus(options =>
{
    options.ConnectionString = "Endpoint=sb://mybus.servicebus.windows.net/;SharedAccessKeyName=...";
    options.MaxConcurrentCalls = 10;
    options.PrefetchCount = 20;
    options.MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5);
});
```

### Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string` | required | Azure SB connection string |
| `EnableSessions` | `bool` | `false` | Enable sessions for ordering |
| `MaxConcurrentCalls` | `int` | `10` | Concurrent message handlers |
| `PrefetchCount` | `int` | `20` | Prefetch count |
| `MaxAutoLockRenewalDuration` | `TimeSpan` | `5m` | Lock renewal duration |

## At-Least-Once Delivery

All brokers implement at-least-once delivery semantics:

1. **Kafka**: Manual offset commit after processing
2. **RabbitMQ**: Manual ACK after processing
3. **Azure SB**: CompleteMessageAsync after processing

Handle duplicates using idempotency keys:

```csharp
await broker.SubscribeAsync("orders.created", "handler", async envelope =>
{
    // Check if already processed
    if (await _cache.ExistsAsync($"processed:{envelope.Id}"))
        return;
    
    // Process event
    await ProcessEventAsync(envelope);
    
    // Mark as processed (with TTL)
    await _cache.SetAsync($"processed:{envelope.Id}", "1", TimeSpan.FromHours(24));
});
```

## Choosing a Broker

| Feature | Kafka | RabbitMQ | Azure SB |
|---------|-------|----------|----------|
| **Throughput** | Very High | High | High |
| **Ordering** | Per-partition | Per-queue | With sessions |
| **Replay** | Yes | No | Yes (topics) |
| **Managed** | Confluent Cloud | CloudAMQP | Azure |
| **Complexity** | Higher | Lower | Medium |
| **Best For** | Event streaming | Task queues | Azure apps |
