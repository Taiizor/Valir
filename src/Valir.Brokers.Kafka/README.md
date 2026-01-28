# Valir.Brokers.Kafka

Apache Kafka adapter for the Valir event bus system.

## Features

- **KafkaEventBroker** - IEventBroker implementation for Kafka
- Consumer groups for scalable message processing
- At-least-once delivery semantics
- SASL authentication support

## Installation

```bash
dotnet add package Valir.Brokers.Kafka
```

## Quick Start

```csharp
builder.Services.AddValirKafka(options =>
{
    options.BootstrapServers = "localhost:9092";
    options.GroupId = "my-consumer-group";
});
```

## Documentation

See the [main documentation](https://github.com/Taiizor/Valir) for complete usage guide.
