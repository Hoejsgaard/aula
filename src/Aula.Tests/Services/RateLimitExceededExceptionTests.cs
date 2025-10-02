using Aula.AI.Services;
using Aula.Content.WeekLetters;
using Aula.Core.Models;
using Aula.Core.Security;
using Aula.Core.Utilities;
using Aula.Core.Security;
using System;
using Xunit;

namespace Aula.Tests.Services;

public class RateLimitExceededExceptionTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesPropertiesCorrectly()
    {
        // Arrange
        const string operation = "GetWeekLetter";
        const string childName = "Emma";
        const int limitPerWindow = 10;
        var windowDuration = TimeSpan.FromMinutes(5);

        // Act
        var exception = new RateLimitExceededException(operation, childName, limitPerWindow, windowDuration);

        // Assert
        Assert.Equal(operation, exception.Operation);
        Assert.Equal(childName, exception.ChildName);
        Assert.Equal(limitPerWindow, exception.LimitPerWindow);
        Assert.Equal(windowDuration, exception.WindowDuration);
    }

    [Fact]
    public void Constructor_WithValidParameters_SetsMessageCorrectly()
    {
        // Arrange
        const string operation = "GetWeekLetter";
        const string childName = "Emma";
        const int limitPerWindow = 10;
        var windowDuration = TimeSpan.FromMinutes(5);
        var expectedMessage = $"Rate limit exceeded for operation '{operation}' by {childName}. Limit: {limitPerWindow} per {windowDuration.TotalMinutes} minutes";

        // Act
        var exception = new RateLimitExceededException(operation, childName, limitPerWindow, windowDuration);

        // Assert
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void Constructor_InheritsFromException()
    {
        // Arrange & Act
        var exception = new RateLimitExceededException("test", "test", 1, TimeSpan.FromMinutes(1));

        // Assert
        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Fact]
    public void Properties_AreReadOnly()
    {
        // Arrange
        var exceptionType = typeof(RateLimitExceededException);

        // Act & Assert
        var operationProperty = exceptionType.GetProperty("Operation");
        var childNameProperty = exceptionType.GetProperty("ChildName");
        var limitPerWindowProperty = exceptionType.GetProperty("LimitPerWindow");
        var windowDurationProperty = exceptionType.GetProperty("WindowDuration");

        Assert.NotNull(operationProperty);
        Assert.NotNull(childNameProperty);
        Assert.NotNull(limitPerWindowProperty);
        Assert.NotNull(windowDurationProperty);

        Assert.True(operationProperty.CanRead);
        Assert.False(operationProperty.CanWrite);
        Assert.True(childNameProperty.CanRead);
        Assert.False(childNameProperty.CanWrite);
        Assert.True(limitPerWindowProperty.CanRead);
        Assert.False(limitPerWindowProperty.CanWrite);
        Assert.True(windowDurationProperty.CanRead);
        Assert.False(windowDurationProperty.CanWrite);
    }

    [Theory]
    [InlineData("GetWeekLetter", "Emma", 5, 2)]
    [InlineData("SendMessage", "Liam", 100, 60)]
    [InlineData("Login", "Sophia", 3, 15)]
    public void Constructor_WithDifferentValues_CreatesCorrectMessage(string operation, string childName, int limit, int minutes)
    {
        // Arrange
        var windowDuration = TimeSpan.FromMinutes(minutes);
        var expectedMessage = $"Rate limit exceeded for operation '{operation}' by {childName}. Limit: {limit} per {minutes} minutes";

        // Act
        var exception = new RateLimitExceededException(operation, childName, limit, windowDuration);

        // Assert
        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(operation, exception.Operation);
        Assert.Equal(childName, exception.ChildName);
        Assert.Equal(limit, exception.LimitPerWindow);
        Assert.Equal(windowDuration, exception.WindowDuration);
    }

    [Fact]
    public void Constructor_WithEmptyOperation_StillWorks()
    {
        // Arrange
        const string operation = "";
        const string childName = "Emma";
        const int limitPerWindow = 10;
        var windowDuration = TimeSpan.FromMinutes(5);

        // Act
        var exception = new RateLimitExceededException(operation, childName, limitPerWindow, windowDuration);

        // Assert
        Assert.Equal(operation, exception.Operation);
        Assert.Contains("Rate limit exceeded for operation '' by Emma", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyChildName_StillWorks()
    {
        // Arrange
        const string operation = "GetWeekLetter";
        const string childName = "";
        const int limitPerWindow = 10;
        var windowDuration = TimeSpan.FromMinutes(5);

        // Act
        var exception = new RateLimitExceededException(operation, childName, limitPerWindow, windowDuration);

        // Assert
        Assert.Equal(childName, exception.ChildName);
        Assert.Contains("Rate limit exceeded for operation 'GetWeekLetter' by ", exception.Message);
    }

    [Fact]
    public void Constructor_WithZeroLimit_StillWorks()
    {
        // Arrange
        const string operation = "GetWeekLetter";
        const string childName = "Emma";
        const int limitPerWindow = 0;
        var windowDuration = TimeSpan.FromMinutes(5);

        // Act
        var exception = new RateLimitExceededException(operation, childName, limitPerWindow, windowDuration);

        // Assert
        Assert.Equal(limitPerWindow, exception.LimitPerWindow);
        Assert.Contains("Limit: 0 per", exception.Message);
    }

    [Fact]
    public void Constructor_WithZeroDuration_ShowsZeroMinutes()
    {
        // Arrange
        const string operation = "GetWeekLetter";
        const string childName = "Emma";
        const int limitPerWindow = 10;
        var windowDuration = TimeSpan.Zero;

        // Act
        var exception = new RateLimitExceededException(operation, childName, limitPerWindow, windowDuration);

        // Assert
        Assert.Equal(windowDuration, exception.WindowDuration);
        Assert.Contains("per 0 minutes", exception.Message);
    }

    [Fact]
    public void Constructor_WithSecondsTimeSpan_ShowsFractionalMinutes()
    {
        // Arrange
        const string operation = "GetWeekLetter";
        const string childName = "Emma";
        const int limitPerWindow = 10;
        var windowDuration = TimeSpan.FromSeconds(30); // 0.5 minutes

        // Act
        var exception = new RateLimitExceededException(operation, childName, limitPerWindow, windowDuration);

        // Assert
        Assert.Equal(windowDuration, exception.WindowDuration);
        Assert.Contains("per 0.5 minutes", exception.Message);
    }

    [Fact]
    public void Exception_CanBeThrownAndCaught()
    {
        // Arrange
        const string operation = "GetWeekLetter";
        const string childName = "Emma";
        const int limitPerWindow = 10;
        var windowDuration = TimeSpan.FromMinutes(5);

        // Act & Assert
        RateLimitExceededException caughtException = null!;
        try
        {
            throw new RateLimitExceededException(operation, childName, limitPerWindow, windowDuration);
        }
        catch (RateLimitExceededException ex)
        {
            caughtException = ex;
        }

        Assert.NotNull(caughtException);
        Assert.Equal(operation, caughtException.Operation);
        Assert.Equal(childName, caughtException.ChildName);
        Assert.Equal(limitPerWindow, caughtException.LimitPerWindow);
        Assert.Equal(windowDuration, caughtException.WindowDuration);
    }

    [Fact]
    public void Exception_HasCorrectNamespace()
    {
        // Arrange
        var exceptionType = typeof(RateLimitExceededException);

        // Act & Assert
        Assert.Equal("Aula.Services.Exceptions", exceptionType.Namespace);
    }

    [Fact]
    public void Exception_IsPublicClass()
    {
        // Arrange
        var exceptionType = typeof(RateLimitExceededException);

        // Act & Assert
        Assert.True(exceptionType.IsPublic);
        Assert.False(exceptionType.IsAbstract);
        Assert.False(exceptionType.IsSealed);
    }

    [Fact]
    public void Exception_HasCorrectConstructorSignature()
    {
        // Arrange
        var exceptionType = typeof(RateLimitExceededException);
        var constructor = exceptionType.GetConstructor(new[] { typeof(string), typeof(string), typeof(int), typeof(TimeSpan) });
        var parameters = constructor?.GetParameters();

        // Act & Assert
        Assert.NotNull(constructor);
        Assert.Equal(4, parameters!.Length);
        Assert.Equal("operation", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("childName", parameters[1].Name);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal("limitPerWindow", parameters[2].Name);
        Assert.Equal(typeof(int), parameters[2].ParameterType);
        Assert.Equal("windowDuration", parameters[3].Name);
        Assert.Equal(typeof(TimeSpan), parameters[3].ParameterType);
    }
}