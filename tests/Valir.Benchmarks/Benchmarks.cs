using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Valir.Abstractions;
using Valir.Core;

namespace Valir.Benchmarks;

/// <summary>
/// Entry point for running benchmarks.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

/// <summary>
/// Benchmarks for Valir's RetryPolicy exponential backoff calculations.
/// Tests the core retry delay algorithm used in job queue failures.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class RetryPolicyBenchmarks
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(30);

    [Params(0, 1, 5, 10, 15)]
    public int Attempt { get; set; }

    [Benchmark(Baseline = true)]
    public TimeSpan CalculateDelayWithDefaults()
    {
        return RetryPolicy.CalculateDelay(Attempt, BaseDelay);
    }

    [Benchmark]
    public TimeSpan CalculateDelayWithMaxCap()
    {
        return RetryPolicy.CalculateDelay(Attempt, BaseDelay, MaxDelay);
    }
}

/// <summary>
/// Benchmarks for job envelope serialization - critical path for job queue operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class JobEnvelopeSerializationBenchmarks
{
    private JobEnvelope _smallJob = default!;
    private JobEnvelope _mediumJob = default!;
    private JobEnvelope _largeJob = default!;
    private byte[] _serializedSmall = default!;
    private byte[] _serializedMedium = default!;
    private byte[] _serializedLarge = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Small payload: typical notification/email job
        _smallJob = new JobEnvelope(
            Id: Guid.CreateVersion7().ToString("N"),
            Type: "SendEmail",
            Payload: Encoding.UTF8.GetBytes("""{"to":"user@example.com","subject":"Hello"}"""),
            Attempts: 0,
            MaxAttempts: 3,
            CreatedAt: DateTimeOffset.UtcNow,
            VisibilityTimeout: TimeSpan.FromMinutes(5)
        );

        // Medium payload: typical data processing job
        byte[] mediumData = new byte[1024];
        Random.Shared.NextBytes(mediumData);
        _mediumJob = _smallJob with
        {
            Type = "ProcessData",
            Payload = mediumData
        };

        // Large payload: batch job with significant data
        byte[] largeData = new byte[65536]; // 64KB
        Random.Shared.NextBytes(largeData);
        _largeJob = _smallJob with
        {
            Type = "BatchImport",
            Payload = largeData
        };

        // Pre-serialize for deserialization benchmarks
        _serializedSmall = JsonSerializer.SerializeToUtf8Bytes(_smallJob);
        _serializedMedium = JsonSerializer.SerializeToUtf8Bytes(_mediumJob);
        _serializedLarge = JsonSerializer.SerializeToUtf8Bytes(_largeJob);
    }

    [Benchmark(Baseline = true)]
    public byte[] SerializeSmallJob()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_smallJob);
    }

    [Benchmark]
    public byte[] SerializeMediumJob()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_mediumJob);
    }

    [Benchmark]
    public byte[] SerializeLargeJob()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_largeJob);
    }

    [Benchmark]
    public JobEnvelope? DeserializeSmallJob()
    {
        return JsonSerializer.Deserialize<JobEnvelope>(_serializedSmall);
    }

    [Benchmark]
    public JobEnvelope? DeserializeMediumJob()
    {
        return JsonSerializer.Deserialize<JobEnvelope>(_serializedMedium);
    }

    [Benchmark]
    public JobEnvelope? DeserializeLargeJob()
    {
        return JsonSerializer.Deserialize<JobEnvelope>(_serializedLarge);
    }
}

/// <summary>
/// Benchmarks for System.Threading.Channels - core mechanism for Valir's WorkerRuntime backpressure.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ChannelBenchmarks
{
    private Channel<JobEnvelope> _boundedChannel = default!;
    private Channel<JobEnvelope> _unboundedChannel = default!;
    private JobEnvelope _testJob = default!;

    [Params(10, 100, 1000)]
    public int ChannelCapacity { get; set; }

    [IterationSetup]
    public void IterationSetup()
    {
        _boundedChannel = Channel.CreateBounded<JobEnvelope>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _unboundedChannel = Channel.CreateUnbounded<JobEnvelope>();

        _testJob = new JobEnvelope(
            Id: Guid.CreateVersion7().ToString("N"),
            Type: "TestJob",
            Payload: Encoding.UTF8.GetBytes("""{"key":"value"}"""),
            Attempts: 0,
            MaxAttempts: 3,
            CreatedAt: DateTimeOffset.UtcNow,
            VisibilityTimeout: TimeSpan.FromMinutes(5)
        );
    }

    [Benchmark(Baseline = true)]
    public async Task WriteToUnboundedChannel()
    {
        await _unboundedChannel.Writer.WriteAsync(_testJob);
    }

    [Benchmark]
    public async Task WriteToBoundedChannel()
    {
        await _boundedChannel.Writer.WriteAsync(_testJob);
    }

    [Benchmark]
    public bool TryWriteToBoundedChannel()
    {
        return _boundedChannel.Writer.TryWrite(_testJob);
    }
}

/// <summary>
/// Benchmarks for job ID generation - Valir uses Guid.CreateVersion7() for time-ordered IDs.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class JobIdGenerationBenchmarks
{
    [Benchmark(Baseline = true)]
    public string GenerateWithNewGuid()
    {
        return Guid.NewGuid().ToString("N");
    }

    [Benchmark]
    public string GenerateWithVersion7()
    {
        return Guid.CreateVersion7().ToString("N");
    }

    [Benchmark]
    public string GenerateWithVersion7NoFormat()
    {
        return Guid.CreateVersion7().ToString();
    }
}

/// <summary>
/// Benchmarks for ValirOptions - configuration object access patterns.
/// </summary>
[MemoryDiagnoser]
public class ValirOptionsBenchmarks
{
    private ValirOptions _options = default!;

    [GlobalSetup]
    public void Setup()
    {
        _options = new ValirOptions
        {
            Concurrency = 4,
            DefaultMaxAttempts = 5,
            DefaultVisibilityTimeout = TimeSpan.FromMinutes(5),
            PollingInterval = TimeSpan.FromMilliseconds(100),
            ShutdownTimeout = TimeSpan.FromSeconds(30),
            RetryBaseDelay = TimeSpan.FromSeconds(1),
            KeyPrefix = "valir:"
        };
    }

    [Benchmark]
    public TimeSpan AccessVisibilityTimeout()
    {
        return _options.DefaultVisibilityTimeout;
    }

    [Benchmark]
    public int AccessConcurrency()
    {
        return _options.Concurrency;
    }

    [Benchmark]
    public string BuildKeyWithPrefix()
    {
        return $"{_options.KeyPrefix}queue:waiting";
    }
}

/// <summary>
/// Benchmarks for batch job creation - simulates EnqueueBatchAsync preparation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class BatchJobPreparationBenchmarks
{
    [Params(10, 100, 1000)]
    public int BatchSize { get; set; }

    [Benchmark]
    public List<(string type, byte[] payload, string? key)> PrepareJobBatch()
    {
        List<(string type, byte[] payload, string? key)> jobs = new(BatchSize);

        for (int i = 0; i < BatchSize; i++)
        {
            jobs.Add((
                type: "BatchJob",
                payload: Encoding.UTF8.GetBytes($"{{\"index\":{i}}}"),
                key: null
            ));
        }

        return jobs;
    }

    [Benchmark]
    public string[] GenerateJobIds()
    {
        string[] ids = new string[BatchSize];

        for (int i = 0; i < BatchSize; i++)
        {
            ids[i] = Guid.CreateVersion7().ToString("N");
        }

        return ids;
    }

    [Benchmark]
    public long[] CalculatePriorityScores()
    {
        long[] scores = new long[BatchSize];
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int priority = 5;

        for (int i = 0; i < BatchSize; i++)
        {
            scores[i] = (priority * -1_000_000_000L) + now + i;
        }

        return scores;
    }
}
