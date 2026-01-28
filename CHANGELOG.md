# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-01-28

### Added

- **Core Framework**
  - `Valir.Abstractions` - Core interfaces (`IJobQueue`, `IEventBroker`, `IDistributedLock`, `IRateLimiter`)
  - `Valir.Core` - Worker runtime with Channels, retry policies, graceful shutdown
  - `Valir.Redis` - Redis-backed job queue with Lua scripts for atomic operations

- **Event Bus Adapters**
  - `Valir.Brokers.Kafka` - Apache Kafka adapter
  - `Valir.Brokers.RabbitMQ` - RabbitMQ adapter
  - `Valir.Brokers.AzureSB` - Azure Service Bus adapter

- **Integrations**
  - `Valir.AspNet` - ASP.NET Core DI extensions and OpenTelemetry
  - `Valir.EntityFrameworkCore` - Transactional Outbox pattern

- **Worker**
  - `Valir.Sample.Worker` - Standalone CLI sample with Spectre.Console TUI
  - Graceful shutdown with drain mode
  - Configurable concurrency and polling

- **Features**
  - At-least-once delivery with idempotency keys
  - Priority queues (higher priority processed first)
  - Batch enqueue operations with Redis pipelining
  - Distributed locks with auto-renewal
  - Sliding window rate limiter
  - OpenTelemetry native tracing

### Infrastructure

- GitHub Actions CI/CD pipeline
- NuGet package publishing workflow
- Testcontainers for integration tests

[Unreleased]: https://github.com/Taiizor/Valir/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Taiizor/Valir/releases/tag/v1.0.0
