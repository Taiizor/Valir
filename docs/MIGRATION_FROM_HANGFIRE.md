# Hangfire'dan Valir'e Migration Rehberi

> **Hedef Kitle:** Hangfire kullanan .NET geliştiriciler  
> **Öncül Bilgi:** Hangfire API ve .NET Dependency Injection  
> **Tahmini Süre:** 30-60 dakika

---

## 📋 İçindekiler

1. [Giriş ve Motivasyon](#1-giriş-ve-motivasyon)
2. [API Karşılaştırma Tablosu](#2-api-karşılaştırma-tablosu)
3. [Adım Adım Migration](#3-adım-adım-migration)
4. [Kod Dönüştürme Örnekleri](#4-kod-dönüştürme-örnekleri)
5. [Yaygın Pattern'lerin Karşılıkları](#5-yaygın-patternlerin-karşılıkları)
6. [Dashboard Alternatifleri](#6-dashboard-alternatifleri)
7. [Troubleshooting](#7-troubleshooting)

---

## 1. Giriş ve Motivasyon

### Neden Valir'e Geçiş?

| Kriter | Hangfire | Valir |
|--------|----------|-------|
| **Performans** | SQL-based, orta düzey | Redis-based, yüksek performans |
| **Ölçeklenebilirlik** | Sınırlı (SQL locking) | Mükemmel (Redis tabanlı) |
| **Event-Driven** | ❌ Desteklemez | ✅ Kafka/RabbitMQ/Azure SB |
| **Observability** | Temel | OpenTelemetry, Prometheus |
| **Modern .NET** | Eski API patterns | .NET 10, Minimal APIs |
| **Distributed Lock** | ❌ | ✅ RedLock |

### Hangfire'dan Farklar

```
┌─────────────────────────────────────────────────────────────────┐
│                    FARKLILIKLAR                                  │
├─────────────────────────────────────────────────────────────────┤
│  Hangfire                    │  Valir                           │
├─────────────────────────────────────────────────────────────────┤
│  SQL Server/Redis/MongoDB    │  Redis (primary)                 │
│  Expression tree (static)    │  IJobHandler<T> (interface)      │
│  Web Dashboard               │  Terminal UI (TUI)               │
│  Built-in recurring jobs     │  Cronos ile custom (yakında)     │
│  Job filters/attributes      │  Decorator pattern               │
│  Automatic retries           │  Configurable retry policy       │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. API Karşılaştırma Tablosu

### 2.1 Temel İşlemler

| Hangfire API | Valir Eşdeğeri | Açıklama |
|--------------|----------------|----------|
| `BackgroundJob.Enqueue(() => Method())` | `IJobQueue.EnqueueAsync(type, payload)` | Fire-and-forget iş |
| `BackgroundJob.Schedule(() => Method(), delay)` | `IJobQueue.EnqueueAsync(type, payload, delay)` | Gecikmeli iş |
| `RecurringJob.AddOrUpdate(id, () => Method(), cron)` | ⚠️ Yakında (Cronos entegrasyonu) | Tekrarlayan iş |
| `BackgroundJob.ContinueWith(id, () => Method())` | Manuel implementasyon | Zincirleme iş |

### 2.2 Yapılandırma

| Hangfire | Valir |
|----------|-------|
| `services.AddHangfire()` | `services.AddValir(options => {...})` |
| `services.AddHangfireServer()` | `WorkerRuntime` veya `IHostedService` |
| `UseHangfireDashboard()` | Terminal UI (`--interactive`) |

### 2.3 Job Tanımlama

| Hangfire | Valir |
|----------|-------|
| `public void Method()` (herhangi bir metod) | `IJobHandler<TJob>` implementasyonu |
| `[AutomaticRetry]` | `ValirOptions.DefaultMaxAttempts` |
| `[Queue("critical")]` | `IJobQueue.EnqueueAsync` priority parametresi |
| `[DisplayName]` | Job type string'i |

---

## 3. Adım Adım Migration

### Adım 1: NuGet Paket Değişiklikleri

**Kaldırılacak Paketler:**
```bash
dotnet remove package Hangfire
dotnet remove package Hangfire.SqlServer
dotnet remove package Hangfire.Redis  # Varsa
```

**Eklenecek Paketler:**
```bash
# Core paketler
dotnet add package Valir.Redis
dotnet add package Valir.AspNet

# Opsiyonel: Event Bus
dotnet add package Valir.Brokers.Kafka
dotnet add package Valir.Brokers.RabbitMQ
dotnet add package Valir.Brokers.AzureSB

# Opsiyonel: Logging
dotnet add package Valir.Extensions.Serilog
```

### Adım 2: Dependency Injection Yapılandırması

**Hangfire (Eski):**
```csharp
// Program.cs
builder.Services.AddHangfire(config =>
{
    config.UseSqlServerStorage("Server=...;Database=Hangfire;...");
});

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 4;
    options.Queues = new[] { "default", "critical" };
});

app.UseHangfireDashboard("/hangfire");
```

**Valir (Yeni):**
```csharp
// Program.cs
using Valir.AspNet;

builder.Services.AddValir(options =>
{
    options.RedisConnectionString = "localhost:6379";
    options.KeyPrefix = "myapp:";
    options.Concurrency = 4;
    options.DefaultMaxAttempts = 3;
    options.RetryBaseDelay = TimeSpan.FromSeconds(10);
});

// Worker ayrı process'te çalışır (bkz. Adım 4)
```

### Adım 3: Job Handler'ların Oluşturulması

Hangfire'da herhangi bir static metod job olarak kullanılabilirken, Valir'de explicit `IJobHandler<T>` implementasyonu gerekir.

**Hangfire (Eski):**
```csharp
public static class EmailJobs
{
    [AutomaticRetry(Attempts = 3)]
    [Queue("emails")]
    public static void SendWelcomeEmail(int userId)
    {
        var user = GetUser(userId);
        _emailService.Send(user.Email, "Welcome!");
    }
}
```

**Valir (Yeni):**
```csharp
// 1. Job payload tanımı
public record SendWelcomeEmailJob(int UserId);

// 2. Handler implementasyonu
public class SendWelcomeEmailHandler : IJobHandler<SendWelcomeEmailJob>
{
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;

    public SendWelcomeEmailHandler(
        IEmailService emailService,
        IUserRepository userRepository)
    {
        _emailService = emailService;
        _userRepository = userRepository;
    }

    public async Task HandleAsync(SendWelcomeEmailJob job, JobContext context)
    {
        var user = await _userRepository.GetAsync(job.UserId);
        await _emailService.SendAsync(user.Email, "Welcome!");
    }
}

// 3. DI kaydı
builder.Services.AddScoped<IJobHandler<SendWelcomeEmailJob>, SendWelcomeEmailHandler>();
```

### Adım 4: Worker Kurulumu

**Hangfire:**
```csharp
// Otomatik - AddHangfireServer() ile
```

**Valir:**

Ayrı bir Worker projesi oluşturun:

```csharp
// Program.cs (Worker)
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Valir.Redis;
using Valir.Core;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(new ValirOptions
        {
            RedisConnectionString = "localhost:6379",
            Concurrency = 4,
            Queues = new[] { "default", "emails", "critical" }
        });
        
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect("localhost:6379"));
        services.AddSingleton<IJobQueue, RedisJobQueue>();
        
        // Handler'ları kaydet
        services.AddScoped<IJobHandler<SendWelcomeEmailJob>, SendWelcomeEmailHandler>();
        
        services.AddHostedService<WorkerHostedService>();
    })
    .Build();

await host.RunAsync();
```

### Adım 5: Job Dispatching Güncellemesi

**Hangfire (Eski):**
```csharp
public class OrderController : ControllerBase
{
    [HttpPost]
    public IActionResult CreateOrder(CreateOrderRequest request)
    {
        // Fire-and-forget
        BackgroundJob.Enqueue(() => ProcessOrder(request.OrderId));
        
        // Delayed
        BackgroundJob.Schedule(
            () => SendReminder(request.OrderId), 
            TimeSpan.FromHours(24));
        
        return Accepted();
    }
}
```

**Valir (Yeni):**
```csharp
public class OrderController : ControllerBase
{
    private readonly IJobQueue _queue;
    
    public OrderController(IJobQueue queue)
    {
        _queue = queue;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new ProcessOrderJob(request.OrderId));
        
        // Fire-and-forget
        var jobId = await _queue.EnqueueAsync(
            type: "process-order",
            payload: payload,
            priority: 5,
            idempotencyKey: $"order-{request.OrderId}"
        );
        
        // Delayed (24 saat sonra)
        await _queue.EnqueueAsync(
            type: "send-reminder",
            payload: JsonSerializer.SerializeToUtf8Bytes(
                new SendReminderJob(request.OrderId)),
            delay: TimeSpan.FromHours(24),
            idempotencyKey: $"reminder-{request.OrderId}"
        );
        
        return Accepted(new { JobId = jobId });
    }
}
```

---

## 4. Kod Dönüştürme Örnekleri

### 4.1 Enqueue İşlemleri

**Hangfire:**
```csharp
// Basit çağrı
BackgroundJob.Enqueue(() => Console.WriteLine("Hello!"));

// Parametre ile
BackgroundJob.Enqueue(() => ProcessOrder(orderId));

// Generic metod
BackgroundJob.Enqueue(() => ProcessGeneric<Order>(orderData));
```

**Valir:**
```csharp
// Job payload tanımı
public record ProcessOrderJob(int OrderId);

// Handler
public class ProcessOrderHandler : IJobHandler<ProcessOrderJob>
{
    public async Task HandleAsync(ProcessOrderJob job, JobContext context)
    {
        Console.WriteLine($"Processing order: {job.OrderId}");
    }
}

// Enqueue
var payload = JsonSerializer.SerializeToUtf8Bytes(new ProcessOrderJob(orderId));
var jobId = await _queue.EnqueueAsync(
    type: "process-order",
    payload: payload,
    idempotencyKey: $"order-{orderId}"
);
```

### 4.2 Schedule İşlemleri

**Hangfire:**
```csharp
// 5 dakika sonra çalıştır
BackgroundJob.Schedule(
    () => SendReminder(userId), 
    TimeSpan.FromMinutes(5));

// Belirli bir tarihte çalıştır
BackgroundJob.Schedule(
    () => SendBirthdayEmail(userId), 
    new DateTime(2026, 1, 1));
```

**Valir:**
```csharp
// 5 dakika sonra çalıştır
await _queue.EnqueueAsync(
    type: "send-reminder",
    payload: JsonSerializer.SerializeToUtf8Bytes(new SendReminderJob(userId)),
    delay: TimeSpan.FromMinutes(5),
    idempotencyKey: $"reminder-{userId}"
);

// Belirli bir tarihte çalıştır
var delay = birthdayDate - DateTime.UtcNow;
await _queue.EnqueueAsync(
    type: "send-birthday",
    payload: JsonSerializer.SerializeToUtf8Bytes(new SendBirthdayJob(userId)),
    delay: delay,
    idempotencyKey: $"birthday-{userId}-{birthdayDate:yyyy}"
);
```

### 4.3 Recurring Jobs

**Hangfire:**
```csharp
RecurringJob.AddOrUpdate(
    "cleanup", 
    () => CleanupOldData(), 
    Cron.Daily);

RecurringJob.AddOrUpdate(
    "reports", 
    () => GenerateReports(), 
    "0 9 * * 1-5",  // Hafta içi 9:00
    TimeZoneInfo.Local);
```

**Valir (Yakında):**
```csharp
// Cronos entegrasyonu ile (v1.1.0)
// Şu an için özel implementasyon gerekir

// Alternatif: Quartz.NET + Valir kombinasyonu
public class RecurringJobScheduler : IHostedService
{
    private readonly IJobQueue _queue;
    
    public async Task StartAsync(CancellationToken ct)
    {
        // Her gün 02:00'de cleanup job'u schedule et
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                var nextRun = GetNextRunTime("0 2 * * *");
                await Task.Delay(nextRun - DateTime.UtcNow, ct);
                
                await _queue.EnqueueAsync(
                    type: "cleanup",
                    payload: JsonSerializer.SerializeToUtf8Bytes(new CleanupJob()),
                    idempotencyKey: $"cleanup-{DateTime.UtcNow:yyyyMMdd}"
                );
            }
        }, ct);
        
        return Task.CompletedTask;
    }
}
```

### 4.4 Job Continuation (ContinueWith)

**Hangfire:**
```csharp
var jobId = BackgroundJob.Enqueue(() => Step1());
BackgroundJob.ContinueWith(jobId, () => Step2());
BackgroundJob.ContinueWith(jobId, () => Step3());
```

**Valir:**
```csharp
// Manuel implementasyon - Job içinde yeni job enqueue etme
public class Step1Handler : IJobHandler<Step1Job>
{
    private readonly IJobQueue _queue;
    
    public async Task HandleAsync(Step1Job job, JobContext context)
    {
        // Step 1 işlemleri
        await DoStep1();
        
        // Step 2'yi schedule et
        await _queue.EnqueueAsync(
            type: "step2",
            payload: JsonSerializer.SerializeToUtf8Bytes(new Step2Job(job.CorrelationId)),
            idempotencyKey: $"step2-{job.CorrelationId}"
        );
    }
}
```

---

## 5. Yaygın Pattern'lerin Karşılıkları

### 5.1 Retry Politikaları

**Hangfire:**
```csharp
[AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 10, 30, 60 })]
public void ProcessWithRetry() { }
```

**Valir:**
```csharp
// Global ayar (Program.cs)
builder.Services.AddValir(options =>
{
    options.DefaultMaxAttempts = 5;
    options.RetryBaseDelay = TimeSpan.FromSeconds(10);
});

// Veya job-specific retry (handler içinde)
public class ProcessWithRetryHandler : IJobHandler<ProcessJob>
{
    public async Task HandleAsync(ProcessJob job, JobContext context)
    {
        try
        {
            await ProcessAsync();
        }
        catch (Exception ex) when (context.Attempts < 5)
        {
            // Retry delay hesapla
            var delay = RetryPolicy.CalculateDelay(
                context.Attempts, 
                TimeSpan.FromSeconds(10));
            
            throw; // Valir otomatik retry yapacak
        }
    }
}
```

### 5.2 Queue Yönetimi

**Hangfire:**
```csharp
[Queue("critical")]
public void CriticalJob() { }

[Queue("default")]
public void DefaultJob() { }
```

**Valir:**
```csharp
// Priority-based queue (0-9, yüksek = öncelikli)
await _queue.EnqueueAsync(
    type: "critical-job",
    payload: payload,
    priority: 9  // Critical
);

await _queue.EnqueueAsync(
    type: "normal-job",
    payload: payload,
    priority: 5  // Normal
);

await _queue.EnqueueAsync(
    type: "low-job",
    payload: payload,
    priority: 1  // Low
);

// Worker configuration
var options = new ValirOptions
{
    Queues = new[] { "default", "critical", "emails" }
};
```

### 5.3 Job Filters / Attributes

**Hangfire:**
```csharp
public class LogAttribute : JobFilterAttribute, IServerFilter
{
    public void OnPerforming(PerformingContext context)
    {
        Logger.LogInformation("Starting job...");
    }
}

[Log]
public void MyJob() { }
```

**Valir:**
```csharp
// Decorator pattern kullanımı
public class LoggingJobHandler<T> : IJobHandler<T>
{
    private readonly IJobHandler<T> _inner;
    private readonly ILogger<LoggingJobHandler<T>> _logger;
    
    public async Task HandleAsync(T job, JobContext context)
    {
        _logger.LogInformation("Starting job {JobId}...", context.JobId);
        
        try
        {
            await _inner.HandleAsync(job, context);
            _logger.LogInformation("Job {JobId} completed", context.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", context.JobId);
            throw;
        }
    }
}

// DI registration
builder.Services.Decorate<IJobHandler<ProcessJob>, LoggingJobHandler<ProcessJob>>();

// Veya Valir.Extensions.Serilog kullan
builder.Services.AddValirSerilog();
```

### 5.4 Dependency Injection Job'larda

**Hangfire:**
```csharp
public void ProcessOrder(int orderId, [FromServices] IOrderService service)
{
    service.Process(orderId);
}
```

**Valir:**
```csharp
// Constructor injection (standart .NET DI)
public class ProcessOrderHandler : IJobHandler<ProcessOrderJob>
{
    private readonly IOrderService _orderService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessOrderHandler> _logger;
    
    public ProcessOrderHandler(
        IOrderService orderService,
        IEmailService emailService,
        ILogger<ProcessOrderHandler> logger)
    {
        _orderService = orderService;
        _emailService = emailService;
        _logger = logger;
    }
    
    public async Task HandleAsync(ProcessOrderJob job, JobContext context)
    {
        await _orderService.ProcessAsync(job.OrderId);
        await _emailService.SendConfirmationAsync(job.OrderId);
    }
}
```

---

## 6. Dashboard Alternatifleri

### 6.1 Hangfire Dashboard

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthFilter() }
});
```

**Özellikler:**
- Web tabanlı UI
- Job history ve retry
- Real-time monitoring
- Manual job trigger

### 6.2 Valir Terminal UI (TUI)

```bash
# Interactive mode (TUI)
dotnet run --project Worker -- --redis localhost:6379 --concurrency 4

# Headless mode (production)
dotnet run --project Worker -- --redis localhost:6379 --headless
```

**Özellikler:**
- Terminal tabanlı UI
- Real-time job processing görünümü
- Queue statistics
- Graceful shutdown desteği

### 6.3 Monitoring Alternatifleri

**Prometheus + Grafana:**
```csharp
builder.Services.AddValir(options =>
{
    options.AutoRegisterHealthChecks = true;
});

app.MapHealthChecks("/health");
app.MapMetrics(); // Prometheus metrics
```

**OpenTelemetry:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Valir");
        tracing.AddOtlpExporter();
    });
```

---

## 7. Troubleshooting

### 7.1 Sık Karşılaşılan Hatalar

| Hata | Sebep | Çözüm |
|------|-------|-------|
| `RedisConnectionException` | Redis erişilemez | Connection string kontrolü, Redis çalışıyor mu? |
| `JobHandlerNotFound` | Handler DI'ye kaydedilmemiş | `AddScoped<IJobHandler<T>, Handler>()` ekle |
| `SerializationException` | Payload deserialize edilemiyor | JSON formatı ve property isimlerini kontrol et |
| `LockTimeoutException` | İş çok uzun sürdü | `VisibilityTimeout` artır veya heartbeat implemente et |

### 7.2 Performans Sorunları

**Hangfire'dan farklı davranışlar:**

```csharp
// Problem: Çok fazla job enqueue ediliyor
// Çözüm: Batch enqueue kullan
var jobs = requests.Select(r => (
    type: "process",
    payload: JsonSerializer.SerializeToUtf8Bytes(r),
    idempotencyKey: (string?)null
));

var jobIds = await _queue.EnqueueBatchAsync(jobs, priority: 5);
```

### 7.3 Migration Checklist

- [ ] NuGet paketlerini güncelle
- [ ] `IJobHandler<T>` implementasyonları oluştur
- [ ] Static metodları instance handler'lara dönüştür
- [ ] `[AutomaticRetry]` → `ValirOptions.DefaultMaxAttempts`
- [ ] `[Queue]` → `priority` parametresi
- [ ] Job dispatching kodlarını `IJobQueue` kullanacak şekilde güncelle
- [ ] Worker projesi oluştur
- [ ] Dashboard yerine monitoring (Prometheus/Grafana) kur
- [ ] Redis altyapısını hazırla
- [ ] Staging ortamında test et

### 7.4 Geri Dönüş (Rollback) Planı

```csharp
// Feature flag ile gradual migration
if (FeatureFlags.UseValir)
{
    await _valirQueue.EnqueueAsync(...);
}
else
{
    BackgroundJob.Enqueue(() => ...);
}
```

---

## 📚 Ek Kaynaklar

- [Valir Getting Started](./getting-started.md)
- [Valir API Reference](./api-reference.md)
- [Valir Configuration](./configuration.md)
- [Redis Best Practices](https://redis.io/docs/management/)

---

**Son Güncelleme:** Ocak 2026  
**Valir Versiyon:** 1.0.0  
**Hedef .NET:** .NET 10.0
