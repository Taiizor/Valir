using Valir.Core;

namespace Valir.Tests;

public class RetryPolicyTests
{
    [Fact]
    public void CalculateDelay_FirstAttempt_ReturnsBaseDelay()
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(10);

        TimeSpan delay = RetryPolicy.CalculateDelay(0, baseDelay);

        // First attempt: 10s base + up to 2.5s jitter
        Assert.True(delay >= TimeSpan.FromSeconds(10));
        Assert.True(delay <= TimeSpan.FromSeconds(12.5));
    }

    [Fact]
    public void CalculateDelay_SecondAttempt_ReturnsExponentialDelay()
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(10);

        TimeSpan delay = RetryPolicy.CalculateDelay(1, baseDelay);

        // Second attempt: 20s base (10 * 2^1) + jitter
        Assert.True(delay >= TimeSpan.FromSeconds(20));
        Assert.True(delay <= TimeSpan.FromSeconds(22.5));
    }

    [Fact]
    public void CalculateDelay_ThirdAttempt_ReturnsQuadrupleBase()
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(10);

        TimeSpan delay = RetryPolicy.CalculateDelay(2, baseDelay);

        // Third attempt: 40s base (10 * 2^2) + jitter
        Assert.True(delay >= TimeSpan.FromSeconds(40));
        Assert.True(delay <= TimeSpan.FromSeconds(42.5));
    }

    [Fact]
    public void CalculateDelay_RespectsMaxDelay()
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(10);
        TimeSpan maxDelay = TimeSpan.FromSeconds(60);

        TimeSpan delay = RetryPolicy.CalculateDelay(10, baseDelay, maxDelay);

        // Should be capped at 60s
        Assert.True(delay <= maxDelay);
    }

    [Fact]
    public void CalculateDelay_NeverNegative()
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(1);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            TimeSpan delay = RetryPolicy.CalculateDelay(attempt, baseDelay);
            Assert.True(delay >= TimeSpan.Zero);
        }
    }

    [Fact]
    public void CalculateDelay_IncludesJitter()
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(10);
        List<TimeSpan> delays = [];

        for (int i = 0; i < 100; i++)
        {
            delays.Add(RetryPolicy.CalculateDelay(0, baseDelay));
        }

        // With jitter, not all delays should be identical
        int uniqueDelays = delays.Distinct().Count();
        Assert.True(uniqueDelays > 1, "Jitter should produce varied delays");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 20)]
    [InlineData(2, 40)]
    [InlineData(3, 80)]
    public void CalculateDelay_ExponentialBackoff_CorrectBase(int attempt, double expectedBaseSeconds)
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(10);

        TimeSpan delay = RetryPolicy.CalculateDelay(attempt, baseDelay, TimeSpan.FromHours(1));

        // Should be at least the expected base (before jitter)
        Assert.True(delay >= TimeSpan.FromSeconds(expectedBaseSeconds));
    }
}
