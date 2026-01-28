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
    Task HandleAsync(TJob job, JobContext context);
}
