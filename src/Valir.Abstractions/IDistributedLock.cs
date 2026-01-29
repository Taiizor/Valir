namespace Valir.Abstractions;

/// <summary>
/// Distributed lock for coordinating access across workers.
/// </summary>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    /// The resource key being locked.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// The owner identifier (typically worker ID).
    /// </summary>
    string Owner { get; }

    /// <summary>
    /// Attempt to acquire the lock with specified TTL.
    /// </summary>
    /// <param name="ttl">Time-to-live for the lock.</param>
    /// <returns>True if lock was acquired, false if already held.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when ttl is negative or zero.</exception>
    Task<bool> AcquireAsync(TimeSpan ttl);

    /// <summary>
    /// Extend the lock TTL (must be current owner).
    /// </summary>
    /// <param name="ttl">New TTL duration.</param>
    /// <returns>True if extended, false if ownership lost.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when ttl is negative or zero.</exception>
    Task<bool> ExtendAsync(TimeSpan ttl);

    /// <summary>
    /// Release the lock (must be current owner).
    /// </summary>
    /// <returns>A task representing the asynchronous release operation.</returns>
    Task ReleaseAsync();
}
