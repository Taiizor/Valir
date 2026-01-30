# Valir.Extensions.Serilog

Serilog integration for the Valir job queue framework. Provides structured logging with job context enrichment for background job execution.

## Features

- **Structured Logging** - Rich log output with job context (JobId, JobName, WorkerId, Attempt)
- **Job Context Enrichment** - Automatic enrichment of all logs with job execution metadata
- **Configurable Log Levels** - Separate log levels for job start, completion, failure, and retry events
- **Timing Information** - Optional execution duration tracking
- **Payload Logging** - Optional job payload serialization (use with caution)
- **Job Type Filtering** - Exclude specific job types from logging
- **Decorator Pattern** - Non-invasive logging via IJobHandler decoration

## Installation

```bash
dotnet add package Valir.Extensions.Serilog
```

## Quick Start

### Basic Setup

```csharp
using Valir.Extensions.Serilog;

// Add Serilog to Valir
services.AddValir()
    .AddValirSerilog();

// Register job handlers
services.AddSingleton<IJobHandler<MyJob>, MyJobHandler>();

// Apply logging decoration (call after all handlers are registered)
services.UseSerilogForJobs();
```

### Custom Configuration

```csharp
services.AddValir()
    .AddValirSerilog(options =>
    {
        options.MinimumLogLevel = Serilog.Events.LogEventLevel.Debug;
        options.JobStartLogLevel = Serilog.Events.LogEventLevel.Information;
        options.JobCompleteLogLevel = Serilog.Events.LogEventLevel.Information;
        options.JobFailureLogLevel = Serilog.Events.LogEventLevel.Error;
        options.JobRetryLogLevel = Serilog.Events.LogEventLevel.Warning;
        options.EnrichWithJobContext = true;
        options.IncludeTiming = true;
    });
```

### Using with Custom Serilog Logger

```csharp
// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

// Add to Valir
services.AddValir()
    .AddValirSerilog(Log.Logger);
```

### Fluent API

```csharp
services.AddValir()
    .UseSerilog((context, logger) =>
    {
        logger.Information("Job {JobId} started by worker {WorkerId}",
            context.JobId, context.WorkerId);
    });
```

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `MinimumLogLevel` | `Information` | Minimum log level for job execution logs |
| `JobStartLogLevel` | `Information` | Log level for job start events |
| `JobCompleteLogLevel` | `Information` | Log level for job completion events |
| `JobFailureLogLevel` | `Error` | Log level for job failure events |
| `JobRetryLogLevel` | `Warning` | Log level for job retry events |
| `EnrichWithJobContext` | `true` | Whether to enrich logs with job context |
| `LogJobPayload` | `false` | Whether to log job payload (use with caution) |
| `MaxPayloadLogLength` | `1000` | Maximum length of job payload to log |
| `IncludeTiming` | `true` | Whether to include timing information |
| `JobIdPropertyName` | `"JobId"` | Custom property name for JobId |
| `JobNamePropertyName` | `"JobName"` | Custom property name for JobName |
| `WorkerIdPropertyName` | `"WorkerId"` | Custom property name for WorkerId |
| `AttemptPropertyName` | `"Attempt"` | Custom property name for Attempt |
| `JobTypeFilter` | `null` | Filter function to exclude certain job types |

## Job Type Filtering

Exclude specific job types from logging:

```csharp
services.AddValirSerilog(options =>
{
    options.JobTypeFilter = jobName => !jobName.Contains("HeartbeatJob");
});
```

## Log Output Examples

### Job Start
```
[18:30:45 INF] Job MyApp.Jobs.SendEmailJob started (Attempt 1) {"JobId": "job-123", "JobName": "MyApp.Jobs.SendEmailJob", "WorkerId": "worker-1", "Attempt": 1}
```

### Job Complete
```
[18:30:47 INF] Job MyApp.Jobs.SendEmailJob completed in 245ms (Attempt 1) {"JobId": "job-123", "JobName": "MyApp.Jobs.SendEmailJob", "WorkerId": "worker-1", "Attempt": 1}
```

### Job Failure
```
[18:30:47 ERR] Job MyApp.Jobs.SendEmailJob failed after 245ms (Attempt 1) {"JobId": "job-123", "JobName": "MyApp.Jobs.SendEmailJob", "WorkerId": "worker-1", "Attempt": 1}
System.Net.Http.HttpRequestException: Connection refused
   at MyApp.Jobs.SendEmailJob.HandleAsync(SendEmailJob job, JobContext context)
```

## Custom Logging in Job Handlers

Access the enriched logger within your job handlers:

```csharp
public class MyJobHandler : IJobHandler<MyJob>
{
    private readonly ValirSerilogLogger _logger;

    public MyJobHandler(ValirSerilogLogger logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(MyJob job, JobContext context)
    {
        // Get logger enriched with job context
        var log = _logger.ForContext(context, nameof(MyJob));

        log.Information("Processing order {OrderId} for customer {CustomerId}",
            job.OrderId, job.CustomerId);

        // Your job logic here

        log.Information("Order {OrderId} processed successfully", job.OrderId);
    }
}
```

## Integration with Serilog.Sinks

Works seamlessly with any Serilog sink:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/valir-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Seq("http://localhost:5341")
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200")))
    .CreateLogger();
```

## Documentation

See the [main documentation](https://github.com/Taiizor/Valir) for complete usage guide.
