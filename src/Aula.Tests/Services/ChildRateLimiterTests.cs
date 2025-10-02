using Aula.Configuration;
using Aula.AI.Services;
using Aula.Content.WeekLetters;
using Aula.Core.Models;
using Aula.Core.Security;
using Aula.Core.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aula.Tests.Services;

public class ChildRateLimiterTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly ChildRateLimiter _rateLimiter;
    private readonly Child _testChild;

    public ChildRateLimiterTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
        _rateLimiter = new ChildRateLimiter(_mockLoggerFactory.Object);
        _testChild = new Child { FirstName = "Test", LastName = "Child" };
    }

    [Fact]
    public async Task IsAllowedAsync_UnderLimit_ReturnsTrue()
    {
        // Act
        var result = await _rateLimiter.IsAllowedAsync(_testChild, "GetWeekLetter");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAllowedAsync_AfterRecordingUnderLimit_ReturnsTrue()
    {
        // Arrange - Record 50 operations (under limit of 100)
        for (int i = 0; i < 50; i++)
        {
            await _rateLimiter.RecordOperationAsync(_testChild, "GetWeekLetter");
        }

        // Act
        var result = await _rateLimiter.IsAllowedAsync(_testChild, "GetWeekLetter");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAllowedAsync_AtLimit_ReturnsFalse()
    {
        // Arrange - Record 100 operations (at limit)
        for (int i = 0; i < 100; i++)
        {
            await _rateLimiter.RecordOperationAsync(_testChild, "GetWeekLetter");
        }

        // Act
        var result = await _rateLimiter.IsAllowedAsync(_testChild, "GetWeekLetter");

        // Assert
        Assert.False(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Rate limit exceeded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IsAllowedAsync_DifferentOperations_HaveSeparateLimits()
    {
        // Arrange - Max out GetWeekLetter limit
        for (int i = 0; i < 100; i++)
        {
            await _rateLimiter.RecordOperationAsync(_testChild, "GetWeekLetter");
        }

        // Act - Different operation should still be allowed
        var result = await _rateLimiter.IsAllowedAsync(_testChild, "GetWeekSchedule");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAllowedAsync_DifferentChildren_HaveSeparateLimits()
    {
        // Arrange
        var child1 = new Child { FirstName = "Child1", LastName = "Test" };
        var child2 = new Child { FirstName = "Child2", LastName = "Test" };

        // Max out limit for child1
        for (int i = 0; i < 100; i++)
        {
            await _rateLimiter.RecordOperationAsync(child1, "GetWeekLetter");
        }

        // Act - Child2 should still be allowed
        var result = await _rateLimiter.IsAllowedAsync(child2, "GetWeekLetter");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetRemainingOperationsAsync_WithNoOperations_ReturnsFullLimit()
    {
        // Act
        var remaining = await _rateLimiter.GetRemainingOperationsAsync(_testChild, "GetWeekLetter");

        // Assert
        Assert.Equal(100, remaining); // Default limit for GetWeekLetter
    }

    [Fact]
    public async Task GetRemainingOperationsAsync_AfterSomeOperations_ReturnsCorrectRemaining()
    {
        // Arrange - Record 30 operations
        for (int i = 0; i < 30; i++)
        {
            await _rateLimiter.RecordOperationAsync(_testChild, "GetWeekLetter");
        }

        // Act
        var remaining = await _rateLimiter.GetRemainingOperationsAsync(_testChild, "GetWeekLetter");

        // Assert
        Assert.Equal(70, remaining); // 100 - 30
    }

    [Fact]
    public async Task GetRemainingOperationsAsync_AtLimit_ReturnsZero()
    {
        // Arrange - Record 100 operations
        for (int i = 0; i < 100; i++)
        {
            await _rateLimiter.RecordOperationAsync(_testChild, "GetWeekLetter");
        }

        // Act
        var remaining = await _rateLimiter.GetRemainingOperationsAsync(_testChild, "GetWeekLetter");

        // Assert
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task ResetLimitsAsync_ClearsAllLimitsForChild()
    {
        // Arrange - Max out multiple operations
        for (int i = 0; i < 100; i++)
        {
            await _rateLimiter.RecordOperationAsync(_testChild, "GetWeekLetter");
            await _rateLimiter.RecordOperationAsync(_testChild, "GetWeekSchedule");
        }

        // Verify limits are exceeded
        Assert.False(await _rateLimiter.IsAllowedAsync(_testChild, "GetWeekLetter"));
        Assert.False(await _rateLimiter.IsAllowedAsync(_testChild, "GetWeekSchedule"));

        // Act - Reset limits
        await _rateLimiter.ResetLimitsAsync(_testChild);

        // Assert - Both operations should be allowed again
        Assert.True(await _rateLimiter.IsAllowedAsync(_testChild, "GetWeekLetter"));
        Assert.True(await _rateLimiter.IsAllowedAsync(_testChild, "GetWeekSchedule"));
    }

    [Fact]
    public async Task ResetLimitsAsync_OnlyAffectsSpecifiedChild()
    {
        // Arrange
        var child1 = new Child { FirstName = "Child1", LastName = "Test" };
        var child2 = new Child { FirstName = "Child2", LastName = "Test" };

        // Max out limits for both children
        for (int i = 0; i < 100; i++)
        {
            await _rateLimiter.RecordOperationAsync(child1, "GetWeekLetter");
            await _rateLimiter.RecordOperationAsync(child2, "GetWeekLetter");
        }

        // Act - Reset only child1's limits
        await _rateLimiter.ResetLimitsAsync(child1);

        // Assert
        Assert.True(await _rateLimiter.IsAllowedAsync(child1, "GetWeekLetter")); // Reset
        Assert.False(await _rateLimiter.IsAllowedAsync(child2, "GetWeekLetter")); // Still limited
    }

    [Fact]
    public async Task DestructiveOperations_HaveLowerLimits()
    {
        // Arrange - DeleteWeekLetter has limit of 5
        for (int i = 0; i < 5; i++)
        {
            await _rateLimiter.RecordOperationAsync(_testChild, "DeleteWeekLetter");
        }

        // Act
        var result = await _rateLimiter.IsAllowedAsync(_testChild, "DeleteWeekLetter");

        // Assert
        Assert.False(result); // Should be at limit after only 5 operations
    }

    [Fact]
    public async Task DatabaseOperations_HaveLowerLimits()
    {
        // Arrange - StoreWeekLetter has limit of 10
        for (int i = 0; i < 10; i++)
        {
            await _rateLimiter.RecordOperationAsync(_testChild, "StoreWeekLetter");
        }

        // Act
        var result = await _rateLimiter.IsAllowedAsync(_testChild, "StoreWeekLetter");

        // Assert
        Assert.False(result); // Should be at limit after 10 operations
    }

    [Fact]
    public async Task UnknownOperations_UseDefaultLimit()
    {
        // Arrange - Unknown operation uses default limit of 50
        for (int i = 0; i < 50; i++)
        {
            await _rateLimiter.RecordOperationAsync(_testChild, "UnknownOperation");
        }

        // Act
        var result = await _rateLimiter.IsAllowedAsync(_testChild, "UnknownOperation");

        // Assert
        Assert.False(result); // Should be at limit after 50 operations
    }

    [Fact]
    public async Task IsAllowedAsync_WithNullChild_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _rateLimiter.IsAllowedAsync(null!, "GetWeekLetter"));
    }

    [Fact]
    public async Task RecordOperationAsync_WithNullChild_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _rateLimiter.RecordOperationAsync(null!, "GetWeekLetter"));
    }

    [Fact]
    public async Task GetRemainingOperationsAsync_WithNullChild_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _rateLimiter.GetRemainingOperationsAsync(null!, "GetWeekLetter"));
    }

    [Fact]
    public async Task ResetLimitsAsync_WithNullChild_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _rateLimiter.ResetLimitsAsync(null!));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ChildRateLimiter(null!));
    }
}
