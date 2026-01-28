using Valir.Core;

namespace Valir.Tests;

public class JobStateTests
{
    [Fact]
    public void JobState_HasWaiting()
    {
        Assert.Equal(0, (int)JobState.Waiting);
    }

    [Fact]
    public void JobState_HasActive()
    {
        Assert.True(Enum.IsDefined(typeof(JobState), JobState.Active));
    }

    [Fact]
    public void JobState_HasRetry()
    {
        Assert.True(Enum.IsDefined(typeof(JobState), JobState.Retry));
    }

    [Fact]
    public void JobState_HasCompleted()
    {
        Assert.True(Enum.IsDefined(typeof(JobState), JobState.Completed));
    }

    [Fact]
    public void JobState_HasDead()
    {
        Assert.True(Enum.IsDefined(typeof(JobState), JobState.Dead));
    }

    [Fact]
    public void JobState_AllValuesAreDefined()
    {
        JobState[] states = Enum.GetValues<JobState>();

        Assert.Equal(5, states.Length);
        Assert.Contains(JobState.Waiting, states);
        Assert.Contains(JobState.Active, states);
        Assert.Contains(JobState.Retry, states);
        Assert.Contains(JobState.Completed, states);
        Assert.Contains(JobState.Dead, states);
    }

    [Theory]
    [InlineData(JobState.Waiting, "Waiting")]
    [InlineData(JobState.Active, "Active")]
    [InlineData(JobState.Retry, "Retry")]
    [InlineData(JobState.Completed, "Completed")]
    [InlineData(JobState.Dead, "Dead")]
    public void JobState_ToStringReturnsName(JobState state, string expectedName)
    {
        Assert.Equal(expectedName, state.ToString());
    }

    [Fact]
    public void JobState_CanParseFromString()
    {
        JobState parsed = Enum.Parse<JobState>("Active");
        Assert.Equal(JobState.Active, parsed);
    }
}
