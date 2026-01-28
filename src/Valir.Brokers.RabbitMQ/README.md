# Valir.Brokers.RabbitMQ

RabbitMQ adapter for the Valir event bus system.

## Features

- **RabbitMQEventBroker** - IEventBroker implementation for RabbitMQ
- Topic exchanges for flexible routing
- Durable queues for message persistence
- Manual acknowledgments for reliable processing

## Installation

```bash
dotnet add package Valir.Brokers.RabbitMQ
```

## Quick Start

```csharp
builder.Services.AddValirRabbitMQ(options =>
{
    options.HostName = "localhost";
    options.UserName = "guest";
    options.Password = "guest";
});
```

## Documentation

See the [main documentation](https://github.com/Taiizor/Valir) for complete usage guide.
