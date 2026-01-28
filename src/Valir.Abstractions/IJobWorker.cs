namespace Valir.Abstractions;

/// <summary>
/// Interface for background job worker lifecycle.
/// </summary>
public interface IJobWorker
{
    /// <summary>
    /// Unique identifier for this worker instance.
    /// </summary>
    string WorkerId { get; }

    /// <summary>
    /// Start the worker processing loop.
    /// </summary>
    /// <param name="ct">Cancellation token for graceful shutdown.</param>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Stop the worker, draining current jobs before shutdown.
    /// </summary>
    /// <param name="ct">Cancellation token with shutdown timeout.</param>
    Task StopAsync(CancellationToken ct);
}
