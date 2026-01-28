using Valir.Core;

namespace Valir.Tests;

public class ValirOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveSensibleDefaults()
    {
        ValirOptions options = new();

        Assert.Equal("localhost:6379", options.RedisConnectionString);
        Assert.Equal("valir:", options.KeyPrefix);
        Assert.Equal(TimeSpan.FromSeconds(30), options.DefaultVisibilityTimeout);
        Assert.Equal(3, options.DefaultMaxAttempts);
        Assert.Equal(4, options.Concurrency);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ShutdownTimeout);
        Assert.Contains("default", options.Queues);
    }

    [Fact]
    public void Options_CanBeConfigured()
    {
        ValirOptions options = new()
        {
            RedisConnectionString = "redis.example.com:6379",
            Concurrency = 8,
            DefaultMaxAttempts = 5
        };

        Assert.Equal("redis.example.com:6379", options.RedisConnectionString);
        Assert.Equal(8, options.Concurrency);
        Assert.Equal(5, options.DefaultMaxAttempts);
    }
}
