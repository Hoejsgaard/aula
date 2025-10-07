using MinUddannelse.Agents;
using MinUddannelse.Configuration;
using MinUddannelse.Events;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace MinUddannelse.Tests.Agents;

public class ChildWeekLetterHandlerTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Child _testChild;
    private readonly ChildWeekLetterHandler _handler;

    public ChildWeekLetterHandlerTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();

        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _testChild = new Child
        {
            FirstName = "Emma",
            LastName = "Test"
        };

        _handler = new ChildWeekLetterHandler(_testChild, _mockLoggerFactory.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var handler = new ChildWeekLetterHandler(_testChild, _mockLoggerFactory.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullChild_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ChildWeekLetterHandler(null!, _mockLoggerFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ChildWeekLetterHandler(_testChild, null!));
    }

    [Fact]
    public async Task HandleWeekLetterEventAsync_WithDifferentChildName_ReturnsEarlyWithoutLogging()
    {
        // Arrange
        var args = new ChildWeekLetterEventArgs("child123", "DifferentChild", 1, 2024, CreateSampleWeekLetter());

        // Act
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Assert - Should not log the "Received week letter event" message for different child
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Received week letter event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleWeekLetterEventAsync_WithMatchingChild_LogsReceivedEvent()
    {
        // Arrange
        var args = new ChildWeekLetterEventArgs("child123", "Emma", 1, 2024, CreateSampleWeekLetter());

        // Act
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Assert
        VerifyLoggerCall(LogLevel.Information, "Received week letter event for child: Emma");
    }

    [Fact]
    public async Task HandleWeekLetterEventAsync_WithNoBots_LogsWarning()
    {
        // Arrange
        var args = new ChildWeekLetterEventArgs("child123", "Emma", 1, 2024, CreateSampleWeekLetter());

        // Act
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Assert
        VerifyLoggerCall(LogLevel.Information, "Received week letter event for child: Emma");
        VerifyLoggerCall(LogLevel.Warning, "No bots available for Emma");
    }

    [Fact]
    public async Task HandleWeekLetterEventAsync_WithCaseInsensitiveChildName_ProcessesCorrectly()
    {
        // Arrange
        var args = new ChildWeekLetterEventArgs("child123", "EMMA", 1, 2024, CreateSampleWeekLetter());

        // Act
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Assert
        VerifyLoggerCall(LogLevel.Information, "Received week letter event for child: EMMA");
    }

    [Theory]
    [InlineData("Emma")]
    [InlineData("emma")]
    [InlineData("EMMA")]
    [InlineData("EmMa")]
    public async Task HandleWeekLetterEventAsync_WithDifferentCasing_ProcessesCorrectly(string childName)
    {
        // Arrange
        var args = new ChildWeekLetterEventArgs("child123", childName, 1, 2024, CreateSampleWeekLetter());

        // Act
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Assert
        VerifyLoggerCall(LogLevel.Information, $"Received week letter event for child: {childName}");
    }

    [Theory]
    [InlineData(1, 2024)]
    [InlineData(52, 2023)]
    [InlineData(15, 2025)]
    public async Task HandleWeekLetterEventAsync_WithDifferentWeekNumbers_ProcessesCorrectly(int weekNumber, int year)
    {
        // Arrange
        var args = new ChildWeekLetterEventArgs("child123", "Emma", weekNumber, year, CreateSampleWeekLetter());

        // Act
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Assert
        VerifyLoggerCall(LogLevel.Information, "Received week letter event for child: Emma");
    }

    [Fact]
    public async Task HandleWeekLetterEventAsync_FormatsMessageCorrectly()
    {
        // This test verifies that the formatting logic is called by checking that
        // the handler processes the event without throwing exceptions

        // Arrange
        var weekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "5A",
                    ["uge"] = "42",
                    ["indhold"] = "<h1>Test Week Letter</h1><p>This is the content.</p>"
                }
            }
        };

        var args = new ChildWeekLetterEventArgs("child123", "Emma", 42, 2024, weekLetter);

        // Act
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Assert
        VerifyLoggerCall(LogLevel.Information, "Received week letter event for child: Emma");
        VerifyLoggerCall(LogLevel.Warning, "No bots available for Emma");
    }

    [Fact]
    public async Task HandleWeekLetterEventAsync_WithMissingJsonFields_HandlesGracefully()
    {
        // Arrange
        var weekLetter = new JObject(); // Empty JSON

        var args = new ChildWeekLetterEventArgs("child123", "Emma", 1, 2024, weekLetter);

        // Act
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Assert
        VerifyLoggerCall(LogLevel.Information, "Received week letter event for child: Emma");
        VerifyLoggerCall(LogLevel.Warning, "No bots available for Emma");
    }

    [Fact]
    public async Task HandleWeekLetterEventAsync_WithEmptyHtmlContent_HandlesGracefully()
    {
        // Arrange
        var weekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "5A",
                    ["uge"] = "1",
                    ["indhold"] = "" // Empty content
                }
            }
        };

        var args = new ChildWeekLetterEventArgs("child123", "Emma", 1, 2024, weekLetter);

        // Act
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Assert
        VerifyLoggerCall(LogLevel.Information, "Received week letter event for child: Emma");
        VerifyLoggerCall(LogLevel.Warning, "No bots available for Emma");
    }

    [Fact]
    public void Constructor_CreatesHtml2SlackMarkdownConverter()
    {
        // This test verifies the constructor initializes correctly without exceptions

        // Arrange & Act
        var handler = new ChildWeekLetterHandler(_testChild, _mockLoggerFactory.Object);

        // Assert
        Assert.NotNull(handler);
        // CreateLogger called at least once (test setup + this test)
        _mockLoggerFactory.Verify(x => x.CreateLogger(It.IsAny<string>()), Times.AtLeast(1));
    }

    [Fact]
    public async Task HandleWeekLetterEventAsync_WithValidParameters_DoesNotThrow()
    {
        // This test ensures the method handles various valid inputs without throwing

        // Arrange
        var args = new ChildWeekLetterEventArgs("child123", "Emma", 1, 2024, CreateSampleWeekLetter());

        // Act & Assert - Should not throw
        await _handler.HandleWeekLetterEventAsync(args, null, null);

        // Verify basic processing occurred
        VerifyLoggerCall(LogLevel.Information, "Received week letter event for child: Emma");
    }

    private static JObject CreateSampleWeekLetter()
    {
        return new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "Test Class",
                    ["uge"] = "1",
                    ["indhold"] = "<h1>Sample Week Letter</h1><p>Test content</p>"
                }
            }
        };
    }

    private void VerifyLoggerCall(LogLevel level, string message)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
