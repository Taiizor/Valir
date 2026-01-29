namespace Valir.Abstractions;

/// <summary>
/// Handler contract for processing jobs of a specific type.
/// Handlers must be idempotent for at-least-once delivery guarantees.
/// </summary>
/// <typeparam name="TJob">The deserialized job payload type.</typeparam>
public interface IJobHandler<TJob>
{
    /// <summary>
    /// Process the job. Must be idempotent.
    /// Use JobContext.LockOwnerToken as a fencing token for external writes.
    /// </summary>
    /// <param name="job">Deserialized job payload.</param>
    /// <param name="context">Execution context with metadata.</param>
    /// <returns>A task representing the asynchronous job handling operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when job is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via context.CancellationToken.</exception>
    Task HandleAsync(TJob job, JobContext context);
}
