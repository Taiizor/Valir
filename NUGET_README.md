# Valir

A modular .NET distributed framework for background jobs and event-driven architectures.

## Features

- **At-Least-Once Delivery** with idempotency keys
- **Priority Queues** - higher priority jobs processed first
- **Batch Operations** - enqueue thousands of jobs efficiently
- **Graceful Shutdown** - drain mode for zero job loss
- **Transactional Outbox** - atomic job creation with your DB
- **Distributed Locks** - Redis-backed coordination
- **Rate Limiting** - sliding window algorithm
- **OpenTelemetry** - native tracing support

## Quick Start

```bash
dotnet add package Valir.Redis
dotnet add package Valir.AspNet
```

### Configure Services

```csharp
builder.Services.AddValir(options =>
{
    options.RedisConnectionString = "localhost:6379";
});
```

### Enqueue a Job

```csharp
app.MapPost("/jobs", async (IJobQueue queue) =>
{
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { Email = "user@example.com" });
    string jobId = await queue.EnqueueAsync("send-email", payload);
    return Results.Ok(new { JobId = jobId });
});
```

### Run the Worker

```csharp
// Define your job handler
public class EmailJobHandler : IJobWorker
{
    public string JobType => "send-email";

    public async Task ExecuteAsync(JobEnvelope job, CancellationToken ct)
    {
        var data = JsonSerializer.Deserialize<EmailData>(job.Payload);
        await SendEmailAsync(data, ct);
    }
}

// Register and run
builder.Services.AddValir(options =>
{
    options.RedisConnectionString = "localhost:6379";
});

builder.Services.AddSingleton<IJobWorker, EmailJobHandler>();

var worker = app.Services.GetRequiredService<WorkerRuntime>();
await worker.RunAsync();
```

## Packages

| Package | Description |
|---------|-------------|
| Valir.Abstractions | Core interfaces (IJobQueue, IEventBroker) |
| Valir.Core | Worker runtime, retry policies |
| Valir.Redis | Redis job queue implementation |
| Valir.AspNet | ASP.NET Core integration |
| Valir.EntityFrameworkCore | Transactional Outbox pattern |
| Valir.Brokers.Kafka | Apache Kafka adapter |
| Valir.Brokers.RabbitMQ | RabbitMQ adapter |
| Valir.Brokers.AzureSB | Azure Service Bus adapter |

## Configuration

```csharp
builder.Services.AddValir(options =>
{
    options.RedisConnectionString = "localhost:6379";
    options.KeyPrefix = "valir:";
    options.Concurrency = 4;
    options.DefaultMaxAttempts = 3;
    options.RetryBaseDelay = TimeSpan.FromSeconds(10);
    options.DefaultVisibilityTimeout = TimeSpan.FromSeconds(30);
});
```

## Event Bus

### Kafka

```csharp
builder.Services.AddValirKafka(options =>
{
    options.BootstrapServers = "localhost:9092";
    options.GroupId = "my-service";
});
```

### RabbitMQ

```csharp
builder.Services.AddValirRabbitMQ(options =>
{
    options.HostName = "localhost";
    options.UserName = "guest";
    options.Password = "guest";
});
```

### Azure Service Bus

```csharp
builder.Services.AddValirAzureServiceBus(options =>
{
    options.ConnectionString = "Endpoint=sb://...";
});
```

## Transactional Outbox

```csharp
builder.Services.AddValirOutbox<AppDbContext>();

// Jobs are written to outbox table (same transaction)
await _outboxQueue.EnqueueAsync("process-order", payload);
await _context.SaveChangesAsync(); // Atomic!
```

## Documentation

- GitHub: https://github.com/Taiizor/Valir
- Docs: https://github.com/Taiizor/Valir/tree/develop/docs

## License

MIT
