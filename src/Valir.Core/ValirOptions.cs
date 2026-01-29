namespace Valir.Core;

/// <summary>
/// Configuration options for Valir.
/// </summary>
public sealed class ValirOptions
{
    /// <summary>
    /// Redis connection string.
    /// </summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Key prefix for all Redis keys. Default: "valir:".
    /// </summary>
    public string KeyPrefix { get; set; } = "valir:";

    /// <summary>
    /// Default visibility timeout for claimed jobs.
    /// </summary>
    public TimeSpan DefaultVisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum retry attempts before dead-lettering.
    /// </summary>
    public int DefaultMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff on retries.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of concurrent job executions per worker.
    /// </summary>
    public int Concurrency { get; set; } = 4;

    /// <summary>
    /// Timeout for graceful shutdown (drain mode).
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Queues to process. Default: ["default"].
    /// </summary>
    public string[] Queues { get; set; } = ["default"];

    /// <summary>
    /// Polling interval when no jobs are available.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(100);
}
