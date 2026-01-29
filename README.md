<p align="center">
  <img src="icon.png" alt="Valir Logo" width="128" height="128">
</p>

<h1 align="center">Valir</h1>

<p align="center">
  <strong>A modular .NET distributed framework for background jobs and event-driven architectures.</strong>
</p>

<p align="center">
  <a href="https://github.com/Taiizor/Valir/actions/workflows/ci.yml">
    <img src="https://github.com/Taiizor/Valir/actions/workflows/ci.yml/badge.svg" alt="Build">
  </a>
  <a href="https://www.nuget.org/packages/Valir.Redis">
    <img src="https://img.shields.io/nuget/v/Valir.Redis?color=blue&label=NuGet" alt="NuGet">
  </a>
  <a href="https://www.nuget.org/packages/Valir.Redis">
    <img src="https://img.shields.io/nuget/dt/Valir.Redis?color=green&label=Downloads" alt="Downloads">
  </a>
  <a href="https://dotnet.microsoft.com">
    <img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET">
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License">
  </a>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#packages">Packages</a> •
  <a href="#documentation">Documentation</a> •
  <a href="#contributing">Contributing</a>
</p>

---

## Features

| Feature | Description |
|---------|-------------|
| ✅ **At-Least-Once Delivery** | Idempotency keys for reliable processing |
| ✅ **Priority Queues** | Higher priority jobs processed first |
| ✅ **Batch Operations** | Enqueue thousands of jobs efficiently |
| ✅ **Graceful Shutdown** | Drain mode for zero job loss |
| ✅ **Transactional Outbox** | Atomic job creation with your DB |
| ✅ **Distributed Locks** | Redis-backed coordination |
| ✅ **Rate Limiting** | Sliding window algorithm |
| ✅ **OpenTelemetry** | Native tracing support |
| ✅ **TUI Dashboard** | Interactive worker console |

## Quick Start

### Installation

```bash
# Core packages
dotnet add package Valir.Redis
dotnet add package Valir.AspNet

# Event Bus (choose one)
dotnet add package Valir.Brokers.Kafka
dotnet add package Valir.Brokers.RabbitMQ
dotnet add package Valir.Brokers.AzureSB
```

### Producer (Web API)

```csharp
// Program.cs
builder.Services.AddValir(options =>
{
    options.RedisConnectionString = "localhost:6379";
});

// Enqueue a job
app.MapPost("/send-email", async (IJobQueue queue, EmailRequest req) =>
{
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(req);
    string jobId = await queue.EnqueueAsync(
        type: "send-email",
        payload: payload,
        priority: 5,
        idempotencyKey: $"email-{req.UserId}"
    );
    return Results.Ok(new { JobId = jobId });
});
```

### Consumer (Worker)

```csharp
// Define your job handler
public class EmailJobHandler : IJobWorker
{
    public string JobType => "send-email";

    public async Task ExecuteAsync(JobEnvelope job, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<EmailRequest>(job.Payload);
        
        // Process the job
        await SendEmailAsync(request, ct);
        
        Console.WriteLine($"Email sent to {request.Email}");
    }
}

// Program.cs - Register and run
builder.Services.AddValir(options =>
{
    options.RedisConnectionString = "localhost:6379";
    options.Concurrency = 4;
});

builder.Services.AddSingleton<IJobWorker, EmailJobHandler>();

// Start worker runtime
var worker = app.Services.GetRequiredService<WorkerRuntime>();
await worker.RunAsync();
```

Or use the sample worker with TUI:

```bash
dotnet run --project samples/Valir.Sample.Worker -- --redis localhost:6379 --concurrency 4
```

## Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| **Valir.Abstractions** | Core interfaces | [![NuGet](https://img.shields.io/nuget/v/Valir.Abstractions)](https://www.nuget.org/packages/Valir.Abstractions) |
| **Valir.Core** | Worker runtime, retry policies | [![NuGet](https://img.shields.io/nuget/v/Valir.Core)](https://www.nuget.org/packages/Valir.Core) |
| **Valir.Redis** | Redis job queue, distributed lock | [![NuGet](https://img.shields.io/nuget/v/Valir.Redis)](https://www.nuget.org/packages/Valir.Redis) |
| **Valir.AspNet** | ASP.NET Core integration | [![NuGet](https://img.shields.io/nuget/v/Valir.AspNet)](https://www.nuget.org/packages/Valir.AspNet) |
| **Valir.EntityFrameworkCore** | Transactional Outbox pattern | [![NuGet](https://img.shields.io/nuget/v/Valir.EntityFrameworkCore)](https://www.nuget.org/packages/Valir.EntityFrameworkCore) |
| **Valir.Brokers.Kafka** | Apache Kafka adapter | [![NuGet](https://img.shields.io/nuget/v/Valir.Brokers.Kafka)](https://www.nuget.org/packages/Valir.Brokers.Kafka) |
| **Valir.Brokers.RabbitMQ** | RabbitMQ adapter | [![NuGet](https://img.shields.io/nuget/v/Valir.Brokers.RabbitMQ)](https://www.nuget.org/packages/Valir.Brokers.RabbitMQ) |
| **Valir.Brokers.AzureSB** | Azure Service Bus adapter | [![NuGet](https://img.shields.io/nuget/v/Valir.Brokers.AzureSB)](https://www.nuget.org/packages/Valir.Brokers.AzureSB) |

## Architecture

```
┌─────────────┐    ┌───────────────┐    ┌─────────────┐
│   Web API   │───▶│     Redis     │◀───│   Worker    │
│  (Producer) │    │ (Job Queue)   │    │ (Consumer)  │
└─────────────┘    └───────────────┘    └─────────────┘
       │                                       │
       ▼                                       ▼
┌─────────────┐                        ┌─────────────┐
│  Event Bus  │                        │   Handler   │
│ Kafka/RMQ   │                        │ (Your Code) │
└─────────────┘                        └─────────────┘
```

## Configuration

```csharp
services.AddValir(options =>
{
    // Redis connection
    options.RedisConnectionString = "localhost:6379";
    options.KeyPrefix = "valir:";
    
    // Worker settings
    options.Concurrency = 4;
    options.DefaultMaxAttempts = 3;
    options.RetryBaseDelay = TimeSpan.FromSeconds(10);
    
    // Timeouts
    options.DefaultVisibilityTimeout = TimeSpan.FromSeconds(30);
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});
```

## Event Bus Adapters

<details>
<summary><strong>Kafka</strong></summary>

```csharp
services.AddValirKafka(options =>
{
    options.BootstrapServers = "localhost:9092";
    options.GroupId = "my-service";
    options.EnableAutoCommit = false; // At-least-once
});
```
</details>

<details>
<summary><strong>RabbitMQ</strong></summary>

```csharp
services.AddValirRabbitMQ(options =>
{
    options.HostName = "localhost";
    options.UserName = "guest";
    options.Password = "guest";
    options.ExchangeName = "valir.events";
});
```
</details>

<details>
<summary><strong>Azure Service Bus</strong></summary>

```csharp
services.AddValirAzureServiceBus(options =>
{
    options.ConnectionString = "Endpoint=sb://...";
    options.MaxConcurrentCalls = 10;
});
```
</details>

## Transactional Outbox

Ensure job creation is atomic with your database transaction:

```csharp
// Register outbox
services.AddValirOutbox<AppDbContext>();

// In your service
public async Task CreateOrderAsync(Order order)
{
    _context.Orders.Add(order);
    
    // Job is written to outbox table (same transaction)
    await _outboxQueue.EnqueueAsync("process-order", payload);
    
    await _context.SaveChangesAsync(); // Atomic!
}
// Background processor pushes to Redis automatically
```

## Documentation

- [Getting Started](docs/getting-started.md)
- [Configuration](docs/configuration.md)
- [Event Bus](docs/event-bus.md)
- [Transactional Outbox](docs/outbox.md)
- [API Reference](docs/api-reference.md)
- [Docker](docs/docker.md)

## Contributing

Contributions are welcome! Please read our [Contributing Guide](.github/CONTRIBUTING.md) for details.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

<p align="center">
  Made with ❤️ by <a href="https://github.com/Taiizor">Taiizor</a>
</p>
