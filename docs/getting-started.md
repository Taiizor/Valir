# Getting Started with Valir

This guide will help you set up Valir in your .NET application.

## Prerequisites

- .NET 10.0 SDK or later
- Redis 6.0+ (for job queue)
- Optional: Kafka, RabbitMQ, or Azure Service Bus (for event bus)

## Installation

### Core Packages

```bash
# Required for basic job queue functionality
dotnet add package Valir.Redis
dotnet add package Valir.AspNet
```

### Event Bus Adapters (Optional)

```bash
# Choose based on your message broker
dotnet add package Valir.Brokers.Kafka
dotnet add package Valir.Brokers.RabbitMQ
dotnet add package Valir.Brokers.AzureSB
```

### Transactional Outbox (Optional)

```bash
dotnet add package Valir.EntityFrameworkCore
```

## Basic Setup

### 1. Configure Services

```csharp
// Program.cs
using Valir.AspNet;

var builder = WebApplication.CreateBuilder(args);

// Add Valir with Redis
builder.Services.AddValir(options =>
{
    options.RedisConnectionString = "localhost:6379";
    options.KeyPrefix = "myapp:";
    options.Concurrency = 4;
});

var app = builder.Build();
```

### 2. Enqueue a Job

```csharp
using Valir.Abstractions;

app.MapPost("/orders", async (IJobQueue queue, CreateOrderRequest req) =>
{
    // Serialize your payload
    var payload = JsonSerializer.SerializeToUtf8Bytes(req);
    
    // Enqueue the job
    var jobId = await queue.EnqueueAsync(
        type: "process-order",
        payload: payload,
        priority: 5,  // Higher = processed first
        idempotencyKey: $"order-{req.OrderId}"  // Prevents duplicates
    );
    
    return Results.Accepted(new { JobId = jobId });
});
```

### 3. Run the Worker

Start the standalone worker to process jobs:

```bash
cd samples/Valir.Sample.Worker
dotnet run -- --redis localhost:6379 --concurrency 4
```

Or run headless (no TUI):

```bash
dotnet run -- --redis localhost:6379 --headless
```

## Worker Options

| Flag | Description | Default |
|------|-------------|---------|
| `--redis` | Redis connection string | `localhost:6379` |
| `--concurrency` | Number of concurrent workers | `4` |
| `--queues` | Comma-separated queue names | `default` |
| `--headless` | Disable TUI, log to console | `false` |
| `--poll-interval` | Polling interval in ms | `1000` |

## Handling Jobs

Jobs are processed by implementing `IJobWorker`:

```csharp
public class OrderProcessor : IJobWorker
{
    public string JobType => "process-order";

    public async Task ExecuteAsync(JobEnvelope job, CancellationToken ct)
    {
        var order = JsonSerializer.Deserialize<CreateOrderRequest>(job.Payload);
        
        // Process the order...
        await ProcessOrderAsync(order, ct);
    }
}
```

Register your workers:

```csharp
builder.Services.AddSingleton<IJobWorker, OrderProcessor>();
builder.Services.AddSingleton<IJobWorker, EmailSender>();
```

## Next Steps

- [Configuration](configuration.md) - Detailed configuration options
- [Event Bus](event-bus.md) - Set up Kafka/RabbitMQ/Azure SB
- [Transactional Outbox](outbox.md) - Atomic job creation with EF Core
- [API Reference](api-reference.md) - Full API documentation
