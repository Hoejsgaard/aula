using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Configuration;

public class TimersTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var timers = new Timers();

        // Assert
        Assert.Equal(10, timers.SchedulingIntervalSeconds);
    }

    [Fact]
    public void SchedulingIntervalSeconds_CanSetAndGetValue()
    {
        // Arrange
        var timers = new Timers();
        var testValue = 15;

        // Act
        timers.SchedulingIntervalSeconds = testValue;

        // Assert
        Assert.Equal(testValue, timers.SchedulingIntervalSeconds);
    }


    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void SchedulingIntervalSeconds_AcceptsVariousValues(int seconds)
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.SchedulingIntervalSeconds = seconds;

        // Assert
        Assert.Equal(seconds, timers.SchedulingIntervalSeconds);
    }



    [Fact]
    public void Timers_HasCorrectNamespace()
    {
        // Arrange
        var type = typeof(Timers);

        // Act & Assert
        Assert.Equal("Aula.Configuration", type.Namespace);
    }

    [Fact]
    public void Timers_IsPublicClass()
    {
        // Arrange
        var type = typeof(Timers);

        // Act & Assert
        Assert.True(type.IsPublic);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void Timers_HasParameterlessConstructor()
    {
        // Arrange
        var type = typeof(Timers);

        // Act
        var constructor = type.GetConstructor(System.Type.EmptyTypes);

        // Assert
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }


    [Fact]
    public void Timers_SupportsReasonableIntervalRanges()
    {
        // Arrange
        var timers = new Timers();

        // Act & Assert - Very short intervals
        timers.SchedulingIntervalSeconds = 1;
        Assert.Equal(1, timers.SchedulingIntervalSeconds);

        // Act & Assert - Medium intervals
        timers.SchedulingIntervalSeconds = 30;
        Assert.Equal(30, timers.SchedulingIntervalSeconds);

        // Act & Assert - Longer intervals
        timers.SchedulingIntervalSeconds = 120;
        Assert.Equal(120, timers.SchedulingIntervalSeconds);
    }

    [Fact]
    public void Timers_CanSetZeroValues()
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.SchedulingIntervalSeconds = 0;

        // Assert
        Assert.Equal(0, timers.SchedulingIntervalSeconds);
    }

    [Fact]
    public void Timers_CanSetNegativeValues()
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.SchedulingIntervalSeconds = -1;

        // Assert
        Assert.Equal(-1, timers.SchedulingIntervalSeconds);
    }

    [Fact]
    public void Timers_DefaultsAreReasonable()
    {
        // Arrange & Act
        var timers = new Timers();

        // Assert
        Assert.True(timers.SchedulingIntervalSeconds > 0);
    }
}
