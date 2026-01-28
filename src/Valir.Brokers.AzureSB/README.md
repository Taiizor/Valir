# Valir.Brokers.AzureSB

Azure Service Bus adapter for the Valir event bus system.

## Features

- **AzureServiceBusEventBroker** - IEventBroker implementation for Azure Service Bus
- Session support for ordered message processing
- Auto-lock renewal for long-running handlers
- Azure-native messaging patterns

## Installation

```bash
dotnet add package Valir.Brokers.AzureSB
```

## Quick Start

```csharp
builder.Services.AddValirAzureServiceBus(options =>
{
    options.ConnectionString = "Endpoint=sb://...";
});
```

## Documentation

See the [main documentation](https://github.com/Taiizor/Valir) for complete usage guide.
