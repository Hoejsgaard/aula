using MinUddannelse.Security;
using MinUddannelse.GoogleCalendar;
using System;
using Xunit;

namespace MinUddannelse.Tests.GoogleCalendar;

public class InvalidCalendarEventExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_InitializesCorrectly()
    {
        // Arrange
        const string message = "Invalid calendar event format";

        // Act
        var exception = new InvalidCalendarEventException(message);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_InitializesCorrectly()
    {
        // Arrange
        const string message = "Invalid calendar event format";
        var innerException = new ArgumentException("Inner exception");

        // Act
        var exception = new InvalidCalendarEventException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException, exception.InnerException);
    }

    [Fact]
    public void Constructor_InheritsFromException()
    {
        // Arrange & Act
        var exception = new InvalidCalendarEventException("test");

        // Assert
        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Fact]
    public void Constructor_WithEmptyMessage_StillWorks()
    {
        // Arrange
        const string message = "";

        // Act
        var exception = new InvalidCalendarEventException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Constructor_WithNullMessage_StillWorks()
    {
        // Arrange & Act
        var exception = new InvalidCalendarEventException(null!);

        // Assert
        // Exception base class provides default message when null is passed
        Assert.NotNull(exception.Message);
        Assert.Contains("InvalidCalendarEventException", exception.Message);
    }

    [Fact]
    public void Constructor_WithNullInnerException_StillWorks()
    {
        // Arrange
        const string message = "Test message";

        // Act
        var exception = new InvalidCalendarEventException(message, null!);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Theory]
    [InlineData("Calendar event has invalid start date")]
    [InlineData("Missing required field: Title")]
    [InlineData("Event duration cannot be negative")]
    [InlineData("Invalid recurrence pattern")]
    public void Constructor_WithDifferentMessages_CreatesCorrectException(string message)
    {
        // Act
        var exception = new InvalidCalendarEventException(message);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_PreservesInnerExceptionType()
    {
        // Arrange
        const string message = "Calendar validation failed";
        var innerException = new InvalidOperationException("Operation not supported");

        // Act
        var exception = new InvalidCalendarEventException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("Operation not supported", exception.InnerException.Message);
    }

    [Fact]
    public void Exception_CanBeThrownAndCaught_MessageOnly()
    {
        // Arrange
        const string message = "Invalid calendar event";

        // Act & Assert
        InvalidCalendarEventException caughtException = null!;
        try
        {
            throw new InvalidCalendarEventException(message);
        }
        catch (InvalidCalendarEventException ex)
        {
            caughtException = ex;
        }

        Assert.NotNull(caughtException);
        Assert.Equal(message, caughtException.Message);
    }

    [Fact]
    public void Exception_CanBeThrownAndCaught_MessageAndInnerException()
    {
        // Arrange
        const string message = "Calendar event validation failed";
        var innerException = new FormatException("Invalid date format");

        // Act & Assert
        InvalidCalendarEventException caughtException = null!;
        try
        {
            throw new InvalidCalendarEventException(message, innerException);
        }
        catch (InvalidCalendarEventException ex)
        {
            caughtException = ex;
        }

        Assert.NotNull(caughtException);
        Assert.Equal(message, caughtException.Message);
        Assert.Equal(innerException, caughtException.InnerException);
    }

    [Fact]
    public void Exception_HasCorrectNamespace()
    {
        // Arrange
        var exceptionType = typeof(InvalidCalendarEventException);

        // Act & Assert
        Assert.Equal("MinUddannelse.GoogleCalendar", exceptionType.Namespace);
    }

    [Fact]
    public void Exception_IsPublicClass()
    {
        // Arrange
        var exceptionType = typeof(InvalidCalendarEventException);

        // Act & Assert
        Assert.True(exceptionType.IsPublic);
        Assert.False(exceptionType.IsAbstract);
        Assert.False(exceptionType.IsSealed);
    }

    [Fact]
    public void Exception_HasCorrectConstructors()
    {
        // Arrange
        var exceptionType = typeof(InvalidCalendarEventException);
        var constructors = exceptionType.GetConstructors();

        // Act & Assert
        Assert.Equal(3, constructors.Length); // Default parameterless, string message, string message + exception

        // Check first constructor (message only)
        var messageConstructor = exceptionType.GetConstructor(new[] { typeof(string) });
        Assert.NotNull(messageConstructor);
        var messageParams = messageConstructor.GetParameters();
        Assert.Single(messageParams);
        Assert.Equal("message", messageParams[0].Name);
        Assert.Equal(typeof(string), messageParams[0].ParameterType);

        // Check second constructor (message and inner exception)
        var fullConstructor = exceptionType.GetConstructor(new[] { typeof(string), typeof(Exception) });
        Assert.NotNull(fullConstructor);
        var fullParams = fullConstructor.GetParameters();
        Assert.Equal(2, fullParams.Length);
        Assert.Equal("message", fullParams[0].Name);
        Assert.Equal(typeof(string), fullParams[0].ParameterType);
        Assert.Equal("innerException", fullParams[1].Name);
        Assert.Equal(typeof(Exception), fullParams[1].ParameterType);
    }

    [Fact]
    public void Exception_InheritsCorrectBaseClass()
    {
        // Arrange
        var exceptionType = typeof(InvalidCalendarEventException);

        // Act & Assert
        Assert.Equal(typeof(Exception), exceptionType.BaseType);
        Assert.True(typeof(Exception).IsAssignableFrom(exceptionType));
    }

    [Fact]
    public void Exception_WithInnerException_CanAccessNestedProperties()
    {
        // Arrange
        var deepInnerException = new ArgumentNullException("parameter", "Parameter cannot be null");
        var innerException = new InvalidOperationException("Operation failed", deepInnerException);
        var outerException = new InvalidCalendarEventException("Calendar event error", innerException);

        // Act & Assert
        Assert.Equal("Calendar event error", outerException.Message);
        Assert.Equal(innerException, outerException.InnerException);
        Assert.Equal("Operation failed", outerException.InnerException.Message);
        Assert.Equal(deepInnerException, outerException.InnerException.InnerException);
        Assert.Contains("Parameter cannot be null", outerException.InnerException.InnerException.Message);
    }
}