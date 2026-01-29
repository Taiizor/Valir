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

    /// <summary>
    /// Maximum polling interval during exponential backoff (when queue is empty).
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan MaxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Backoff multiplier for empty queue polling. Default: 2.0.
    /// </summary>
    public double PollingBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Enable jitter for polling intervals to prevent thundering herd. Default: true.
    /// </summary>
    public bool EnablePollingJitter { get; set; } = true;

    /// <summary>
    /// Maximum payload size in bytes. Default: 10 MB.
    /// </summary>
    public int MaxPayloadSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Heartbeat interval divisor for lock extension.
    /// Heartbeat runs every VisibilityTimeout / HeartbeatIntervalDivisor.
    /// Default: 3 (heartbeat every 1/3 of visibility timeout).
    /// </summary>
    public int HeartbeatIntervalDivisor { get; set; } = 3;

    /// <summary>
    /// Enable automatic lock extension via heartbeat. Default: true.
    /// </summary>
    public bool EnableHeartbeat { get; set; } = true;
}
