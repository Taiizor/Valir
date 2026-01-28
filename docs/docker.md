# Docker Setup

This guide explains how to run Valir infrastructure locally using Docker Compose.

## Prerequisites

- Docker Desktop 4.x+ or Docker Engine 24.x+
- Docker Compose v2

## Quick Start

```bash
# Start all services
docker compose up -d

# Check status
docker compose ps

# View logs
docker compose logs -f
```

## Services

| Service | Port | Description |
|---------|------|-------------|
| Redis | 6379 | Job queue backend |
| Redis Commander | 8081 | Redis web UI |
| RabbitMQ | 5672, 15672 | Event bus (AMQP + Management UI) |
| Kafka | 9092 | Event bus (streaming) |
| Kafka UI | 8082 | Kafka web UI |
| PostgreSQL | 5432 | Database for Outbox pattern |
| Jaeger | 16686 | Distributed tracing UI |

## Access Points

| Service | URL |
|---------|-----|
| Redis Commander | http://localhost:8081 |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| Kafka UI | http://localhost:8082 |
| Jaeger UI | http://localhost:16686 |

## Connection Strings

Use these in your `appsettings.json` or environment variables:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "Database": {
    "ConnectionString": "Host=localhost;Port=5432;Database=valir;Username=valir;Password=valir"
  }
}
```

## Profiles

### Development (All Services)

```bash
docker compose up -d
```

### Redis Only

```bash
docker compose up -d redis redis-commander
```

### Event Bus Only (Kafka + RabbitMQ)

```bash
docker compose up -d kafka kafka-ui rabbitmq
```

### Database Only

```bash
docker compose up -d postgres
```

### Observability Only

```bash
docker compose up -d jaeger
```

## OpenTelemetry Configuration

Configure your application to send traces to Jaeger:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Valir");
        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
        });
    });
```

## Cleanup

```bash
# Stop all services
docker compose down

# Stop and remove volumes (data loss!)
docker compose down -v
```

## Troubleshooting

### Port Conflicts

If ports are in use, modify `docker-compose.yml`:

```yaml
services:
  redis:
    ports:
      - "16379:6379"  # Changed from 6379
```

### Kafka Not Starting

Kafka requires the CLUSTER_ID to be consistent. If having issues:

```bash
docker compose down -v
docker compose up -d kafka
```

### Memory Issues

Kafka and RabbitMQ can be memory-intensive. Allocate at least 4GB to Docker Desktop.

## Integration Tests

The docker-compose stack is configured for integration testing with Testcontainers.
For CI/CD, use the services directly:

```bash
docker compose up -d redis postgres
dotnet test
docker compose down
```
