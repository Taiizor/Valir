using Valir.Abstractions;

namespace Valir.Tests;

/// <summary>
/// Unit tests for MisfirePolicy enum.
/// Tests enum values exist and have correct integer values.
/// </summary>
public class MisfirePolicyTests
{
    #region Enum Value Existence Tests

    [Fact]
    public void MisfirePolicy_HasSkipValue()
    {
        // Arrange & Act
        var policy = MisfirePolicy.Skip;

        // Assert
        Assert.Equal(0, (int)policy);
    }

    [Fact]
    public void MisfirePolicy_HasFireAllValue()
    {
        // Arrange & Act
        var policy = MisfirePolicy.FireAll;

        // Assert
        Assert.Equal(1, (int)policy);
    }

    [Fact]
    public void MisfirePolicy_HasFireOnceValue()
    {
        // Arrange & Act
        var policy = MisfirePolicy.FireOnce;

        // Assert
        Assert.Equal(2, (int)policy);
    }

    [Fact]
    public void MisfirePolicy_HasFireNowValue()
    {
        // Arrange & Act
        var policy = MisfirePolicy.FireNow;

        // Assert
        Assert.Equal(3, (int)policy);
    }

    #endregion

    #region Enum Count Test

    [Fact]
    public void MisfirePolicy_HasFourValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<MisfirePolicy>();

        // Assert
        Assert.Equal(4, values.Length);
    }

    #endregion

    #region Enum Parsing Tests

    [Theory]
    [InlineData("Skip", MisfirePolicy.Skip)]
    [InlineData("FireAll", MisfirePolicy.FireAll)]
    [InlineData("FireOnce", MisfirePolicy.FireOnce)]
    [InlineData("FireNow", MisfirePolicy.FireNow)]
    public void MisfirePolicy_CanBeParsedFromString(string name, MisfirePolicy expected)
    {
        // Act
        var parsed = Enum.Parse<MisfirePolicy>(name);

        // Assert
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(0, MisfirePolicy.Skip)]
    [InlineData(1, MisfirePolicy.FireAll)]
    [InlineData(2, MisfirePolicy.FireOnce)]
    [InlineData(3, MisfirePolicy.FireNow)]
    public void MisfirePolicy_CanBeConvertedFromInt(int value, MisfirePolicy expected)
    {
        // Act
        var policy = (MisfirePolicy)value;

        // Assert
        Assert.Equal(expected, policy);
    }

    #endregion

    #region Enum ToString Tests

    [Theory]
    [InlineData(MisfirePolicy.Skip, "Skip")]
    [InlineData(MisfirePolicy.FireAll, "FireAll")]
    [InlineData(MisfirePolicy.FireOnce, "FireOnce")]
    [InlineData(MisfirePolicy.FireNow, "FireNow")]
    public void MisfirePolicy_ToString_ReturnsCorrectName(MisfirePolicy policy, string expected)
    {
        // Act
        string name = policy.ToString();

        // Assert
        Assert.Equal(expected, name);
    }

    #endregion

    #region Enum Comparison Tests

    [Fact]
    public void MisfirePolicy_Skip_IsNotEqualToFireOnce()
    {
        // Assert
        Assert.NotEqual(MisfirePolicy.Skip, MisfirePolicy.FireOnce);
    }

    [Fact]
    public void MisfirePolicy_SameValues_AreEqual()
    {
        // Arrange
        var policy1 = MisfirePolicy.FireOnce;
        var policy2 = MisfirePolicy.FireOnce;

        // Assert
        Assert.Equal(policy1, policy2);
    }

    [Fact]
    public void MisfirePolicy_CanBeUsedInSwitchStatement()
    {
        // Arrange
        var policy = MisfirePolicy.FireOnce;
        string result = policy switch
        {
            MisfirePolicy.Skip => "skip",
            MisfirePolicy.FireAll => "fire-all",
            MisfirePolicy.FireOnce => "fire-once",
            MisfirePolicy.FireNow => "fire-now",
            _ => "unknown",
        };

        // Assert
        Assert.Equal("fire-once", result);
    }

    [Fact]
    public void MisfirePolicy_CanBeUsedInSwitchExpression()
    {
        // Arrange
        var policy = MisfirePolicy.Skip;

        // Act
        string result = policy switch
        {
            MisfirePolicy.Skip => "skip",
            MisfirePolicy.FireAll => "fire-all",
            MisfirePolicy.FireOnce => "fire-once",
            MisfirePolicy.FireNow => "fire-now",
            _ => "unknown"
        };

        // Assert
        Assert.Equal("skip", result);
    }

    #endregion

    #region Enum Iteration Tests

    [Fact]
    public void MisfirePolicy_AllValues_CanBeIterated()
    {
        // Arrange
        var expectedValues = new[]
        {
            MisfirePolicy.Skip,
            MisfirePolicy.FireAll,
            MisfirePolicy.FireOnce,
            MisfirePolicy.FireNow
        };

        // Act
        var actualValues = Enum.GetValues<MisfirePolicy>();

        // Assert
        Assert.Equal(expectedValues, actualValues);
    }

    [Fact]
    public void MisfirePolicy_AllValues_HaveSequentialIntValues()
    {
        // Arrange
        var values = Enum.GetValues<MisfirePolicy>();

        // Assert
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(i, (int)values[i]);
        }
    }

    #endregion

    #region Enum IsDefined Tests

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(-1, false)]
    [InlineData(100, false)]
    public void MisfirePolicy_IsDefined_ReturnsCorrectResult(int value, bool expected)
    {
        // Act
        bool isDefined = Enum.IsDefined(typeof(MisfirePolicy), value);

        // Assert
        Assert.Equal(expected, isDefined);
    }

    #endregion

    #region Enum TryParse Tests

    [Theory]
    [InlineData("Skip", true, MisfirePolicy.Skip)]
    [InlineData("FireAll", true, MisfirePolicy.FireAll)]
    [InlineData("FireOnce", true, MisfirePolicy.FireOnce)]
    [InlineData("FireNow", true, MisfirePolicy.FireNow)]
    [InlineData("Invalid", false, default(MisfirePolicy))]
    [InlineData("", false, default(MisfirePolicy))]
    public void MisfirePolicy_TryParse_ReturnsCorrectResult(
        string input,
        bool expectedSuccess,
        MisfirePolicy expectedValue)
    {
        // Act
        bool success = Enum.TryParse<MisfirePolicy>(input, out var result);

        // Assert
        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedValue, result);
        }
    }

    #endregion

    #region Type Safety Tests

    [Fact]
    public void MisfirePolicy_IsValueType()
    {
        // Arrange
        var policy = MisfirePolicy.FireOnce;

        // Assert
        Assert.True(policy is ValueType);
        Assert.True(typeof(MisfirePolicy).IsEnum);
    }

    [Fact]
    public void MisfirePolicy_DefaultValue_IsSkip()
    {
        // Arrange
        MisfirePolicy defaultPolicy = default;

        // Assert
        Assert.Equal(MisfirePolicy.Skip, defaultPolicy);
        Assert.Equal(0, (int)defaultPolicy);
    }

    #endregion
}
