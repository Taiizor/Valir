namespace Valir.Abstractions;

/// <summary>
/// Rate limiter for throttling operations.
/// Implements sliding window or token bucket algorithm.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Check if an operation is allowed under the rate limit.
    /// </summary>
    /// <param name="key">Rate limit key (e.g., user ID, API key).</param>
    /// <param name="max">Maximum allowed operations in the window.</param>
    /// <param name="window">Time window for the limit.</param>
    /// <returns>True if allowed, false if rate limited.</returns>
    Task<bool> AllowAsync(string key, int max, TimeSpan window);
}
