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
    /// <returns>A task representing the asynchronous start operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Stop the worker, draining current jobs before shutdown.
    /// </summary>
    /// <param name="ct">Cancellation token with shutdown timeout.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task StopAsync(CancellationToken ct);
}
