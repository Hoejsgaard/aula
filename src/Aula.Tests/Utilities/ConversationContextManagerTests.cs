using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Aula.Utilities;

namespace Aula.Tests.Utilities;

public class ConversationContextManagerTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly ConversationContextManager<string> _manager;

    public ConversationContextManagerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _manager = new ConversationContextManager<string>(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConversationContextManager<string>(null!));
    }

    [Fact]
    public void UpdateContext_WithValidInput_CreatesNewContext()
    {
        // Arrange
        var key = "test-key";
        var childName = TestChild1;

        // Act
        _manager.UpdateContext(key, childName, isAboutToday: true, isAboutTomorrow: false, isAboutHomework: true);

        // Assert
        var context = _manager.GetContext(key);
        Assert.NotNull(context);
        Assert.Equal(childName, context.LastChildName);
        Assert.True(context.WasAboutToday);
        Assert.False(context.WasAboutTomorrow);
        Assert.True(context.WasAboutHomework);
        Assert.True(context.IsStillValid);
    }

    [Fact]
    public void UpdateContext_WithDefaultParameters_SetsCorrectDefaults()
    {
        // Arrange
        var key = "test-key";
        var childName = "TestChild2";

        // Act
        _manager.UpdateContext(key, childName);

        // Assert
        var context = _manager.GetContext(key);
        Assert.NotNull(context);
        Assert.Equal(childName, context.LastChildName);
        Assert.False(context.WasAboutToday);
        Assert.False(context.WasAboutTomorrow);
        Assert.False(context.WasAboutHomework);
    }

    [Fact]
    public void UpdateContext_LogsInformation()
    {
        // Arrange
        var key = "test-key";
        var childName = "Emma";

        // Act
        _manager.UpdateContext(key, childName);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Updated conversation context for key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetContext_WithNonExistentKey_ReturnsNull()
    {
        // Act
        var result = _manager.GetContext("non-existent-key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetContext_WithValidKey_ReturnsContext()
    {
        // Arrange
        var key = "test-key";
        _manager.UpdateContext(key, "TestChild");

        // Act
        var result = _manager.GetContext(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestChild", result.LastChildName);
    }

    [Fact]
    public void GetContext_WithExpiredContext_ReturnsNullAndRemovesContext()
    {
        // Arrange
        var key = "test-key";
        _manager.UpdateContext(key, "TestChild");

        // Manually set the timestamp to be expired (simulate time passing)
        var context = _manager.GetContext(key);
        Assert.NotNull(context);
        context.Timestamp = DateTime.Now.AddMinutes(-11); // Make it expired

        // Act
        var result = _manager.GetContext(key);

        // Assert
        Assert.Null(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Removed expired conversation context for key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ClearContext_WithExistingKey_RemovesContextAndLogs()
    {
        // Arrange
        var key = "test-key";
        _manager.UpdateContext(key, "TestChild");

        // Act
        _manager.ClearContext(key);

        // Assert
        var result = _manager.GetContext(key);
        Assert.Null(result);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleared conversation context for key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ClearContext_WithNonExistentKey_DoesNotLog()
    {
        // Act
        _manager.ClearContext("non-existent-key");

        // Assert - Should not log anything since context didn't exist
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleared conversation context for key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void ClearAllContexts_WithMultipleContexts_RemovesAllAndLogs()
    {
        // Arrange
        _manager.UpdateContext("key1", "Child1");
        _manager.UpdateContext("key2", "Child2");
        _manager.UpdateContext("key3", "Child3");

        // Act
        _manager.ClearAllContexts();

        // Assert
        Assert.Null(_manager.GetContext("key1"));
        Assert.Null(_manager.GetContext("key2"));
        Assert.Null(_manager.GetContext("key3"));

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleared all 3 conversation contexts")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ClearAllContexts_WithNoContexts_LogsZeroCount()
    {
        // Act
        _manager.ClearAllContexts();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleared all 0 conversation contexts")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void UpdateContext_OverwritesExistingContext()
    {
        // Arrange
        var key = "test-key";
        _manager.UpdateContext(key, "FirstChild", isAboutToday: true);

        // Act
        _manager.UpdateContext(key, "SecondChild", isAboutTomorrow: true);

        // Assert
        var context = _manager.GetContext(key);
        Assert.NotNull(context);
        Assert.Equal("SecondChild", context.LastChildName);
        Assert.False(context.WasAboutToday); // Should be reset
        Assert.True(context.WasAboutTomorrow);
        Assert.False(context.WasAboutHomework);
    }

    [Fact]
    public void Manager_WithIntegerKeys_WorksCorrectly()
    {
        // Arrange
        var intManager = new ConversationContextManager<int>(_mockLogger.Object);
        var key = 12345;

        // Act
        intManager.UpdateContext(key, "IntKeyChild", isAboutHomework: true);

        // Assert
        var context = intManager.GetContext(key);
        Assert.NotNull(context);
        Assert.Equal("IntKeyChild", context.LastChildName);
        Assert.True(context.WasAboutHomework);
    }

    [Fact]
    public void Manager_WithGuidKeys_WorksCorrectly()
    {
        // Arrange
        var guidManager = new ConversationContextManager<Guid>(_mockLogger.Object);
        var key = Guid.NewGuid();

        // Act
        guidManager.UpdateContext(key, "GuidKeyChild");

        // Assert
        var context = guidManager.GetContext(key);
        Assert.NotNull(context);
        Assert.Equal("GuidKeyChild", context.LastChildName);
    }

    [Fact]
    public void UpdateContext_WithNullChildName_AllowsNull()
    {
        // Arrange
        var key = "test-key";

        // Act
        _manager.UpdateContext(key, null, isAboutToday: true);

        // Assert
        var context = _manager.GetContext(key);
        Assert.NotNull(context);
        Assert.Null(context.LastChildName);
        Assert.True(context.WasAboutToday);
    }
}