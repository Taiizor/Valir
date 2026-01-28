# Contributing to Valir

Thank you for your interest in contributing to Valir! This document provides guidelines and information for contributors.

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for everyone.

## How to Contribute

### Reporting Bugs

Before creating a bug report, please check existing issues to avoid duplicates.

When creating a bug report, include:
- Clear, descriptive title
- Steps to reproduce the issue
- Expected vs actual behavior
- Environment details (.NET version, OS, Redis version)
- Relevant logs or error messages

### Suggesting Features

Feature requests are welcome! Please:
- Check existing issues and discussions first
- Describe the use case clearly
- Explain why this would benefit other users

### Pull Requests

1. **Fork** the repository
2. **Create a branch** from `develop` for your changes
3. **Write tests** for new functionality
4. **Follow coding standards** (see below)
5. **Update documentation** if needed
6. **Submit a PR** with a clear description

## Development Setup

```bash
# Clone your fork
git clone https://github.com/Taiizor/Valir.git
cd Valir

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test
```

## Coding Standards

- Use C# 14 language features appropriately
- Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use file-scoped namespaces
- Write XML documentation for public APIs
- Keep methods focused and small
- Prefer explicit types for clarity in public APIs

## Project Structure

```
src/
├── Valir.Abstractions/     # Core interfaces
├── Valir.Core/             # Worker runtime
├── Valir.Redis/            # Redis implementation
├── Valir.AspNet/           # ASP.NET integration
├── Valir.EntityFrameworkCore/ # Outbox pattern
├── Valir.Brokers.Kafka/    # Kafka adapter
├── Valir.Brokers.RabbitMQ/ # RabbitMQ adapter
├── Valir.Brokers.AzureSB/  # Azure SB adapter
└── Valir.Worker/           # CLI worker host

samples/
└── Valir.Sample.WebApi/    # Example usage

tests/
└── Valir.Tests/            # Unit & integration tests
```

## Testing

- Write unit tests for new logic
- Use Testcontainers for integration tests
- Ensure all tests pass before submitting PR

```bash
# Run unit tests only
dotnet test --filter "FullyQualifiedName!~Integration"

# Run all tests (requires Docker)
dotnet test
```

## Questions?

Open a [Discussion](https://github.com/Taiizor/Valir/discussions) for questions or ideas.

Thank you for contributing! 🚀
