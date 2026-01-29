namespace Valir.Core;

/// <summary>
/// Calculates retry delays using exponential backoff with jitter.
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// Calculate the delay for the next retry attempt.
    /// Uses exponential backoff: baseDelay * 2^attempt + random jitter.
    /// </summary>
    /// <param name="attempt">Current attempt number (0-based).</param>
    /// <param name="baseDelay">Base delay duration.</param>
    /// <param name="maxDelay">Maximum delay cap.</param>
    /// <returns>Delay before next retry.</returns>
    public static TimeSpan CalculateDelay(int attempt, TimeSpan baseDelay, TimeSpan? maxDelay = null)
    {
        double exponentialMs = baseDelay.TotalMilliseconds * Math.Pow(2, attempt);
        int jitterMs = Random.Shared.Next(0, (int)(baseDelay.TotalMilliseconds * 0.25));
        double totalMs = exponentialMs + jitterMs;

        TimeSpan max = maxDelay ?? TimeSpan.FromMinutes(30);
        return TimeSpan.FromMilliseconds(Math.Min(totalMs, max.TotalMilliseconds));
    }
}
