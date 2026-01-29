# Configuration

Complete configuration reference for Valir.

## ValirOptions

Configure Valir using the `AddValir` extension method:

```csharp
builder.Services.AddValir(options =>
{
    // Connection
    options.RedisConnectionString = "localhost:6379,password=secret,ssl=true";
    options.KeyPrefix = "valir:";
    
    // Worker Settings
    options.Concurrency = 4;
    options.DefaultMaxAttempts = 3;
    options.RetryBaseDelay = TimeSpan.FromSeconds(10);
    
    // Timeouts
    options.DefaultVisibilityTimeout = TimeSpan.FromSeconds(30);
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
    
    // Polling
    options.PollInterval = TimeSpan.FromSeconds(1);
});
```

## Configuration Properties

### Connection Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RedisConnectionString` | `string` | `localhost:6379` | Redis connection string |
| `KeyPrefix` | `string` | `valir:` | Prefix for all Redis keys |

### Worker Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Concurrency` | `int` | `4` | Max concurrent job processors |
| `DefaultMaxAttempts` | `int` | `3` | Max retry attempts per job |
| `RetryBaseDelay` | `TimeSpan` | `10s` | Base delay for exponential backoff |

### Timeout Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultVisibilityTimeout` | `TimeSpan` | `30s` | Time before abandoned job becomes visible |
| `ShutdownTimeout` | `TimeSpan` | `30s` | Grace period for drain mode |

### Heartbeat Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableHeartbeat` | `bool` | `true` | Enable automatic lock extension via heartbeat |
| `HeartbeatIntervalDivisor` | `int` | `3` | Heartbeat runs every VisibilityTimeout / divisor |

### Health Check Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AutoRegisterHealthChecks` | `bool` | `false` | Automatically register Valir health checks with DI |

### Polling Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PollInterval` | `TimeSpan` | `100ms` | Interval between Redis polls |
| `MaxPollingInterval` | `TimeSpan` | `5s` | Maximum polling interval during exponential backoff |
| `PollingBackoffMultiplier` | `double` | `2.0` | Backoff multiplier for empty queue polling |
| `EnablePollingJitter` | `bool` | `true` | Enable jitter for polling intervals to prevent thundering herd |

### Payload Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxPayloadSizeBytes` | `int` | `10MB` | Maximum payload size in bytes (1KB - 100MB) |

## Environment Variables

For production, use environment variables:

```bash
export VALIR_REDIS="redis.example.com:6379,password=secret,ssl=true"
export VALIR_CONCURRENCY=8
export VALIR_KEY_PREFIX="prod:valir:"
```

```csharp
builder.Services.AddValir(options =>
{
    options.RedisConnectionString = 
        Environment.GetEnvironmentVariable("VALIR_REDIS") ?? "localhost:6379";
    options.Concurrency = 
        int.Parse(Environment.GetEnvironmentVariable("VALIR_CONCURRENCY") ?? "4");
});
```

## Redis Connection String

Valir uses StackExchange.Redis format:

```
host:port,password=secret,ssl=true,abortConnect=false
```

### Common Options

| Option | Description |
|--------|-------------|
| `password` | Redis AUTH password |
| `ssl` | Enable TLS/SSL |
| `abortConnect` | Fail fast if cannot connect |
| `connectTimeout` | Connection timeout in ms |
| `syncTimeout` | Sync operation timeout in ms |

### Redis Sentinel

```
sentinel.example.com:26379,serviceName=mymaster,password=secret
```

### Redis Cluster

```
node1:6379,node2:6379,node3:6379
```

## Priority Queues

Higher priority jobs are processed first:

```csharp
// Priority 10 (high) - processed first
await queue.EnqueueAsync("urgent-job", payload, priority: 10);

// Priority 1 (low) - processed last
await queue.EnqueueAsync("batch-job", payload, priority: 1);
```

## Retry Policy

Valir uses exponential backoff with jitter:

```
Delay = BaseDelay × 2^(attempt-1) + random(0, BaseDelay/2)
```

Example with `RetryBaseDelay = 10s`:

| Attempt | ~Delay |
|---------|--------|
| 1 | 10-15s |
| 2 | 20-25s |
| 3 | 40-45s |

## OpenTelemetry

Valir includes native OpenTelemetry support:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Valir");
        tracing.AddOtlpExporter();
    });
```

Activity tags include:
- `valir.job.id`
- `valir.job.type`
- `valir.job.queue`
- `valir.job.attempt`
