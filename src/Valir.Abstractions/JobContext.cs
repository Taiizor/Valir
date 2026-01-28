namespace Valir.Abstractions;

/// <summary>
/// Context passed to job handlers during execution.
/// </summary>
/// <param name="JobId">The unique job identifier.</param>
/// <param name="WorkerId">The worker instance processing this job.</param>
/// <param name="LockOwnerToken">Fencing token for idempotent external writes.</param>
/// <param name="CancellationToken">Token to observe for graceful shutdown.</param>
public sealed record JobContext(
    string JobId,
    string WorkerId,
    string LockOwnerToken,
    CancellationToken CancellationToken
);
