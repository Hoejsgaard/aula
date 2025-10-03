using MinUddannelse.Configuration;
using Xunit;

namespace MinUddannelse.Tests.Configuration;

public class TimeProviderTests
{
    [Fact]
    public void SystemTimeProvider_Now_ReturnsCurrentDateTime()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();
        var beforeCall = DateTime.Now;

        // Act
        var result = timeProvider.Now;
        var afterCall = DateTime.Now;

        // Assert
        Assert.True(result >= beforeCall);
        Assert.True(result <= afterCall);
        Assert.Equal(DateTimeKind.Local, result.Kind);
    }

    [Fact]
    public void SystemTimeProvider_CurrentYear_ReturnsCurrentYear()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();
        var expectedYear = DateTime.Now.Year;

        // Act
        var result = timeProvider.CurrentYear;

        // Assert
        Assert.Equal(expectedYear, result);
        Assert.True(result >= 2020); // Reasonable sanity check
        Assert.True(result <= 2100); // Reasonable sanity check
    }

    [Fact]
    public void SystemTimeProvider_Now_IsConsistentWithDateTime()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();

        // Act
        var providerNow = timeProvider.Now;
        var directNow = DateTime.Now;

        // Assert - Should be within a very small time window
        var timeDifference = Math.Abs((providerNow - directNow).TotalMilliseconds);
        Assert.True(timeDifference < 100); // Less than 100ms difference
    }

    [Fact]
    public void SystemTimeProvider_CurrentYear_IsConsistentWithDateTime()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();

        // Act
        var providerYear = timeProvider.CurrentYear;
        var directYear = DateTime.Now.Year;

        // Assert
        Assert.Equal(directYear, providerYear);
    }

    [Fact]
    public void SystemTimeProvider_MultipleCallsToNow_ReturnConsistentTime()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();

        // Act
        var time1 = timeProvider.Now;
        var time2 = timeProvider.Now;

        // Assert
        // Time should be consistent within microseconds and not go backwards
        Assert.True(time2 >= time1);
        // Verify times are reasonably close (within 1 second)
        Assert.True((time2 - time1).TotalSeconds < 1);
    }

    [Fact]
    public void SystemTimeProvider_MultipleCallsToCurrentYear_ReturnSameYear()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();

        // Act
        var year1 = timeProvider.CurrentYear;
        var year2 = timeProvider.CurrentYear;

        // Assert
        Assert.Equal(year1, year2); // Year shouldn't change during test execution
    }

    [Fact]
    public void SystemTimeProvider_ImplementsITimeProvider()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();

        // Act & Assert
        Assert.IsAssignableFrom<ITimeProvider>(timeProvider);
    }

    [Fact]
    public void SystemTimeProvider_HasCorrectNamespace()
    {
        // Arrange
        var type = typeof(SystemTimeProvider);

        // Act & Assert
        Assert.Equal("MinUddannelse.Configuration", type.Namespace);
    }

    [Fact]
    public void SystemTimeProvider_IsPublicClass()
    {
        // Arrange
        var type = typeof(SystemTimeProvider);

        // Act & Assert
        Assert.True(type.IsPublic);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
        Assert.True(type.IsClass);
    }

    [Fact]
    public void SystemTimeProvider_HasParameterlessConstructor()
    {
        // Arrange
        var type = typeof(SystemTimeProvider);

        // Act
        var constructor = type.GetConstructor(Type.EmptyTypes);

        // Assert
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void SystemTimeProvider_HasNowProperty()
    {
        // Arrange
        var type = typeof(SystemTimeProvider);

        // Act
        var property = type.GetProperty("Now");

        // Assert
        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.Equal(typeof(DateTime), property.PropertyType);
        Assert.True(property.GetMethod!.IsPublic);
    }

    [Fact]
    public void SystemTimeProvider_HasCurrentYearProperty()
    {
        // Arrange
        var type = typeof(SystemTimeProvider);

        // Act
        var property = type.GetProperty("CurrentYear");

        // Assert
        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.Equal(typeof(int), property.PropertyType);
        Assert.True(property.GetMethod!.IsPublic);
    }

    [Fact]
    public void ITimeProvider_IsInterface()
    {
        // Arrange
        var type = typeof(ITimeProvider);

        // Act & Assert
        Assert.True(type.IsInterface);
        Assert.True(type.IsPublic);
        Assert.Equal("MinUddannelse.Configuration", type.Namespace);
    }

    [Fact]
    public void ITimeProvider_HasNowProperty()
    {
        // Arrange
        var type = typeof(ITimeProvider);

        // Act
        var property = type.GetProperty("Now");

        // Assert
        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.Equal(typeof(DateTime), property.PropertyType);
    }

    [Fact]
    public void ITimeProvider_HasCurrentYearProperty()
    {
        // Arrange
        var type = typeof(ITimeProvider);

        // Act
        var property = type.GetProperty("CurrentYear");

        // Assert
        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.Equal(typeof(int), property.PropertyType);
    }

    [Fact]
    public void SystemTimeProvider_CanBeUsedPolymorphically()
    {
        // Arrange
        ITimeProvider timeProvider = new SystemTimeProvider();

        // Act
        var now = timeProvider.Now;
        var currentYear = timeProvider.CurrentYear;

        // Assert
        Assert.NotEqual(default(DateTime), now);
        Assert.True(currentYear > 0);
        Assert.Equal(now.Year, currentYear);
    }

    [Fact]
    public void SystemTimeProvider_PropertiesAreConsistent()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();

        // Act
        var now = timeProvider.Now;
        var currentYear = timeProvider.CurrentYear;

        // Assert
        Assert.Equal(now.Year, currentYear);
    }

    [Fact]
    public void SystemTimeProvider_ReturnsReasonableDateTime()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();

        // Act
        var now = timeProvider.Now;

        // Assert
        Assert.True(now.Year >= 2020); // Should be at least 2020
        Assert.True(now.Year <= 2100); // Should not be too far in future
        Assert.True(now.Month >= 1 && now.Month <= 12);
        Assert.True(now.Day >= 1 && now.Day <= 31);
        Assert.True(now.Hour >= 0 && now.Hour <= 23);
        Assert.True(now.Minute >= 0 && now.Minute <= 59);
        Assert.True(now.Second >= 0 && now.Second <= 59);
    }

    [Fact]
    public void SystemTimeProvider_CanBeInstantiatedMultipleTimes()
    {
        // Arrange & Act
        var provider1 = new SystemTimeProvider();
        var provider2 = new SystemTimeProvider();

        // Assert
        Assert.NotSame(provider1, provider2);

        var time1 = provider1.Now;
        var time2 = provider2.Now;
        var year1 = provider1.CurrentYear;
        var year2 = provider2.CurrentYear;

        // Both should return similar results
        var timeDiff = Math.Abs((time1 - time2).TotalMilliseconds);
        Assert.True(timeDiff < 100); // Should be very close in time
        Assert.Equal(year1, year2); // Should be same year
    }
}
