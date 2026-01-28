# Valir.Redis

Redis implementation for the Valir distributed job queue system.

## Features

- **RedisJobQueue** - Reliable job queue with atomic Lua scripts
- **RedisDistributedLock** - Distributed locking with auto-expiry
- **RedisRateLimiter** - Sliding window rate limiting

## Installation

```bash
dotnet add package Valir.Redis
```

## Quick Start

```csharp
builder.Services.AddValir(options =>
{
    options.RedisConnectionString = "localhost:6379";
});
```

## Documentation

See the [main documentation](https://github.com/Taiizor/Valir) for complete usage guide.
