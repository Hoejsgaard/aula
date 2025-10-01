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
        Assert.Equal(1, timers.CleanupIntervalHours);
        Assert.True(timers.AdaptivePolling);
        Assert.Equal(30, timers.MaxPollingIntervalSeconds);
        Assert.Equal(5, timers.MinPollingIntervalSeconds);
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

    [Fact]
    public void CleanupIntervalHours_CanSetAndGetValue()
    {
        // Arrange
        var timers = new Timers();
        var testValue = 2;

        // Act
        timers.CleanupIntervalHours = testValue;

        // Assert
        Assert.Equal(testValue, timers.CleanupIntervalHours);
    }

    [Fact]
    public void AdaptivePolling_CanSetAndGetValue()
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.AdaptivePolling = false;

        // Assert
        Assert.False(timers.AdaptivePolling);
    }

    [Fact]
    public void MaxPollingIntervalSeconds_CanSetAndGetValue()
    {
        // Arrange
        var timers = new Timers();
        var testValue = 60;

        // Act
        timers.MaxPollingIntervalSeconds = testValue;

        // Assert
        Assert.Equal(testValue, timers.MaxPollingIntervalSeconds);
    }

    [Fact]
    public void MinPollingIntervalSeconds_CanSetAndGetValue()
    {
        // Arrange
        var timers = new Timers();
        var testValue = 1;

        // Act
        timers.MinPollingIntervalSeconds = testValue;

        // Assert
        Assert.Equal(testValue, timers.MinPollingIntervalSeconds);
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(24)]
    public void CleanupIntervalHours_AcceptsVariousValues(int hours)
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.CleanupIntervalHours = hours;

        // Assert
        Assert.Equal(hours, timers.CleanupIntervalHours);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    public void MaxPollingIntervalSeconds_AcceptsVariousValues(int seconds)
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.MaxPollingIntervalSeconds = seconds;

        // Assert
        Assert.Equal(seconds, timers.MaxPollingIntervalSeconds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void MinPollingIntervalSeconds_AcceptsVariousValues(int seconds)
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.MinPollingIntervalSeconds = seconds;

        // Assert
        Assert.Equal(seconds, timers.MinPollingIntervalSeconds);
    }

    [Fact]
    public void AllProperties_CanBeSetSimultaneously()
    {
        // Arrange
        var timers = new Timers();
        var schedulingInterval = 20;
        var cleanupInterval = 3;
        var adaptivePolling = false;
        var maxPollingInterval = 45;
        var minPollingInterval = 2;

        // Act
        timers.SchedulingIntervalSeconds = schedulingInterval;
        timers.CleanupIntervalHours = cleanupInterval;
        timers.AdaptivePolling = adaptivePolling;
        timers.MaxPollingIntervalSeconds = maxPollingInterval;
        timers.MinPollingIntervalSeconds = minPollingInterval;

        // Assert
        Assert.Equal(schedulingInterval, timers.SchedulingIntervalSeconds);
        Assert.Equal(cleanupInterval, timers.CleanupIntervalHours);
        Assert.Equal(adaptivePolling, timers.AdaptivePolling);
        Assert.Equal(maxPollingInterval, timers.MaxPollingIntervalSeconds);
        Assert.Equal(minPollingInterval, timers.MinPollingIntervalSeconds);
    }

    [Fact]
    public void Timers_ObjectInitializerSyntaxWorks()
    {
        // Arrange & Act
        var timers = new Timers
        {
            SchedulingIntervalSeconds = 25,
            CleanupIntervalHours = 4,
            AdaptivePolling = false,
            MaxPollingIntervalSeconds = 90,
            MinPollingIntervalSeconds = 3
        };

        // Assert
        Assert.Equal(25, timers.SchedulingIntervalSeconds);
        Assert.Equal(4, timers.CleanupIntervalHours);
        Assert.False(timers.AdaptivePolling);
        Assert.Equal(90, timers.MaxPollingIntervalSeconds);
        Assert.Equal(3, timers.MinPollingIntervalSeconds);
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
    public void Timers_PropertiesAreIndependent()
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.SchedulingIntervalSeconds = 99;
        // Other properties should remain at their defaults

        // Assert
        Assert.Equal(99, timers.SchedulingIntervalSeconds);
        Assert.Equal(1, timers.CleanupIntervalHours); // Should remain default
        Assert.True(timers.AdaptivePolling); // Should remain default
        Assert.Equal(30, timers.MaxPollingIntervalSeconds); // Should remain default
        Assert.Equal(5, timers.MinPollingIntervalSeconds); // Should remain default
    }

    [Fact]
    public void AdaptivePolling_CanToggle()
    {
        // Arrange
        var timers = new Timers();

        // Act & Assert - Default is true
        Assert.True(timers.AdaptivePolling);

        // Act & Assert - Toggle to false
        timers.AdaptivePolling = false;
        Assert.False(timers.AdaptivePolling);

        // Act & Assert - Toggle back to true
        timers.AdaptivePolling = true;
        Assert.True(timers.AdaptivePolling);
    }

    [Fact]
    public void Timers_SupportsReasonableIntervalRanges()
    {
        // Arrange
        var timers = new Timers();

        // Act & Assert - Very short intervals
        timers.SchedulingIntervalSeconds = 1;
        timers.MinPollingIntervalSeconds = 1;
        Assert.Equal(1, timers.SchedulingIntervalSeconds);
        Assert.Equal(1, timers.MinPollingIntervalSeconds);

        // Act & Assert - Medium intervals
        timers.SchedulingIntervalSeconds = 30;
        timers.MaxPollingIntervalSeconds = 60;
        Assert.Equal(30, timers.SchedulingIntervalSeconds);
        Assert.Equal(60, timers.MaxPollingIntervalSeconds);

        // Act & Assert - Longer intervals
        timers.SchedulingIntervalSeconds = 120;
        timers.MaxPollingIntervalSeconds = 300;
        timers.CleanupIntervalHours = 12;
        Assert.Equal(120, timers.SchedulingIntervalSeconds);
        Assert.Equal(300, timers.MaxPollingIntervalSeconds);
        Assert.Equal(12, timers.CleanupIntervalHours);
    }

    [Fact]
    public void Timers_PollingIntervalPropertiesWorkTogether()
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.AdaptivePolling = true;
        timers.MinPollingIntervalSeconds = 2;
        timers.MaxPollingIntervalSeconds = 60;

        // Assert
        Assert.True(timers.AdaptivePolling);
        Assert.Equal(2, timers.MinPollingIntervalSeconds);
        Assert.Equal(60, timers.MaxPollingIntervalSeconds);
        Assert.True(timers.MinPollingIntervalSeconds < timers.MaxPollingIntervalSeconds);
    }

    [Fact]
    public void Timers_CanSetZeroValues()
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.SchedulingIntervalSeconds = 0;
        timers.CleanupIntervalHours = 0;
        timers.MaxPollingIntervalSeconds = 0;
        timers.MinPollingIntervalSeconds = 0;

        // Assert
        Assert.Equal(0, timers.SchedulingIntervalSeconds);
        Assert.Equal(0, timers.CleanupIntervalHours);
        Assert.Equal(0, timers.MaxPollingIntervalSeconds);
        Assert.Equal(0, timers.MinPollingIntervalSeconds);
    }

    [Fact]
    public void Timers_CanSetNegativeValues()
    {
        // Arrange
        var timers = new Timers();

        // Act
        timers.SchedulingIntervalSeconds = -1;
        timers.CleanupIntervalHours = -2;
        timers.MaxPollingIntervalSeconds = -10;
        timers.MinPollingIntervalSeconds = -3;

        // Assert
        Assert.Equal(-1, timers.SchedulingIntervalSeconds);
        Assert.Equal(-2, timers.CleanupIntervalHours);
        Assert.Equal(-10, timers.MaxPollingIntervalSeconds);
        Assert.Equal(-3, timers.MinPollingIntervalSeconds);
    }

    [Fact]
    public void Timers_DefaultsAreReasonable()
    {
        // Arrange & Act
        var timers = new Timers();

        // Assert - Check that defaults make sense
        Assert.True(timers.SchedulingIntervalSeconds > 0);
        Assert.True(timers.CleanupIntervalHours > 0);
        Assert.True(timers.MaxPollingIntervalSeconds > 0);
        Assert.True(timers.MinPollingIntervalSeconds > 0);
        Assert.True(timers.MaxPollingIntervalSeconds >= timers.MinPollingIntervalSeconds);
        Assert.True(timers.AdaptivePolling);
    }
}
