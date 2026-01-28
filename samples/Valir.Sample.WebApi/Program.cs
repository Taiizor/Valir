using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Valir.Abstractions;
using Valir.AspNet;
using Valir.Brokers.Kafka;
using Valir.Brokers.RabbitMQ;
using Valir.EntityFrameworkCore;
using Valir.Sample.WebApi.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add Valir services
// Add Valir services
builder.Services.AddValir(options =>
{
    options.RedisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString")
                                    ?? "localhost:6379";
});

// Add EF Core Outbox (Postgres)
builder.Services.AddDbContext<ValirSampleDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddValirOutbox<ValirSampleDbContext>();

// Add Brokers (RabbitMQ & Kafka)
builder.Services.AddValirRabbitMQ(options =>
{
    options.HostName = builder.Configuration.GetValue<string>("RabbitMQ:HostName") ?? "localhost";
    options.Port = builder.Configuration.GetValue<int>("RabbitMQ:Port");
    options.UserName = builder.Configuration.GetValue<string>("RabbitMQ:UserName") ?? "guest";
    options.Password = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";
});

builder.Services.AddValirKafka(options =>
{
    options.BootstrapServers = builder.Configuration.GetValue<string>("Kafka:BootstrapServers") ?? "localhost:9092";
    options.GroupId = "valir-sample-group";
});

// Add OpenTelemetry tracing
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Valir.Sample.WebApi"))
    .WithTracing(tracing =>
    {
        tracing.SetSampler(new AlwaysOnSampler());
        tracing.AddSource("Valir");
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
        });
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Redirect root to Swagger UI
    app.MapGet("/", () => Results.Redirect("/swagger"))
       .WithName("RedirectToSwagger");
}

// Job enqueue endpoint
app.MapPost("/jobs", async (IJobQueue queue, JobRequest request) =>
{
    byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request.Data));

    using Activity? activity = ValirTelemetry.StartEnqueue(request.Type);

    string jobId = await queue.EnqueueAsync(
        type: request.Type,
        payload: payload,
        priority: request.Priority,
        idempotencyKey: request.IdempotencyKey
    );

    activity?.SetTag("valir.job.id", jobId);

    return Results.Ok(new { JobId = jobId, Message = "Job enqueued successfully" });
})
.WithName("EnqueueJob");

// Batch enqueue endpoint
app.MapPost("/jobs/batch", async (IJobQueue queue, BatchJobRequest request) =>
{
    IEnumerable<(string type, byte[] payload, string? idempotencyKey)> jobs = request.Jobs.Select(j => (
        type: j.Type,
        payload: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(j.Data)),
        idempotencyKey: j.IdempotencyKey
    ));

    string[] jobIds = await queue.EnqueueBatchAsync(jobs, request.Priority);

    return Results.Ok(new { JobIds = jobIds, Count = jobIds.Length });
})
.WithName("EnqueueBatchJobs");

// Publish event endpoint (for verification)
app.MapPost("/events", async (IEnumerable<IEventBroker> brokers, EventRequest request) =>
{
    using Activity? activity = ValirTelemetry.StartPublish(request.Topic);

    byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request.Data));
    EventEnvelope envelope = new(
        Guid.CreateVersion7().ToString(),
        request.Topic,
        payload,
        DateTimeOffset.UtcNow
    );

    activity?.SetTag("valir.event.id", envelope.Id);
    activity?.SetTag("valir.brokers.count", brokers.Count());

    IEnumerable<Task> publishTasks = brokers.Select(broker => broker.PublishAsync(request.Topic, envelope));
    await Task.WhenAll(publishTasks);

    return Results.Ok(new
    {
        Message = $"Event published to {brokers.Count()} brokers",
        Topic = request.Topic,
        EnvelopeId = envelope.Id,
        Brokers = brokers.Select(b => b.GetType().Name)
    });
})
.WithName("PublishEvent");

// Subscribe endpoint (creates queue for demo)
app.MapPost("/subscribe", async (IEnumerable<IEventBroker> brokers, SubscribeRequest request) =>
{
    using Activity? activity = ValirTelemetry.StartPublish(request.Topic);
    activity?.SetTag("valir.subscription.id", request.SubscriptionId);

    CancellationTokenSource cts = new();
    cts.CancelAfter(TimeSpan.FromSeconds(1)); // Quick cancel for demo

    List<string> subscribedBrokers = [];

    foreach (IEventBroker broker in brokers)
    {
        try
        {
            // This creates the queue/topic in the broker
            _ = broker.SubscribeAsync(
                request.Topic,
                request.SubscriptionId,
                envelope => Task.CompletedTask,
                cts.Token);
            subscribedBrokers.Add(broker.GetType().Name);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    return Results.Ok(new
    {
        Message = "Subscription created (queue bound to exchange)",
        Topic = request.Topic,
        SubscriptionId = request.SubscriptionId,
        Brokers = subscribedBrokers
    });
})
.WithName("Subscribe");

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
.WithName("HealthCheck");

// Ensure DB exists
using (IServiceScope scope = app.Services.CreateScope())
{
    ValirSampleDbContext db = scope.ServiceProvider.GetRequiredService<ValirSampleDbContext>();
    db.Database.EnsureCreated();
}

app.Run();

public record JobRequest(string Type, object? Data = null, int Priority = 0, string? IdempotencyKey = null);
public record BatchJobRequest(JobRequest[] Jobs, int Priority = 0);
public record EventRequest(string Topic, object Data);
public record SubscribeRequest(string Topic, string SubscriptionId);
