# Valir.EntityFrameworkCore

Transactional Outbox pattern implementation for Valir using Entity Framework Core.

## Features

- **OutboxJobQueue** - Atomic job creation with database transactions
- **OutboxProcessor** - Background processor for reliable message delivery
- Ensures consistency between database operations and job enqueuing

## Installation

```bash
dotnet add package Valir.EntityFrameworkCore
```

## Quick Start

```csharp
// Add DbContext with Outbox support
builder.Services.AddDbContext<MyDbContext>(opts => 
    opts.UseNpgsql(connectionString));

builder.Services.AddValirOutbox<MyDbContext>();
```

## Documentation

See the [main documentation](https://github.com/Taiizor/Valir) for complete usage guide.
