using MinUddannelse.Bots;
using MinUddannelse.Configuration;
using MinUddannelse.AI.Services;
using MinUddannelse.Content.WeekLetters;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse.Security;
using MinUddannelse;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace MinUddannelse.Tests.Bots;

public class TelegramInteractiveBotTests
{
    private readonly Mock<IOpenAiService> _mockAiService;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Child _testChild;
    private readonly TelegramInteractiveBot _bot;

    public TelegramInteractiveBotTests()
    {
        _mockAiService = new Mock<IOpenAiService>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();

        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _testChild = new Child
        {
            FirstName = "Emma",
            LastName = "Test",
            Channels = new ChildChannels
            {
                Telegram = new ChildTelegramConfig
                {
                    Token = "test-token",
                    ChatId = 12345
                }
            }
        };

        _bot = new TelegramInteractiveBot(_testChild, _mockAiService.Object, _mockLoggerFactory.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var bot = new TelegramInteractiveBot(_testChild, _mockAiService.Object, _mockLoggerFactory.Object);

        // Assert
        Assert.NotNull(bot);
        Assert.Equal("Emma", bot.AssignedChildName);
    }

    [Fact]
    public void Constructor_WithNullChild_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TelegramInteractiveBot(null!, _mockAiService.Object, _mockLoggerFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullAiService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TelegramInteractiveBot(_testChild, null!, _mockLoggerFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TelegramInteractiveBot(_testChild, _mockAiService.Object, null!));
    }

    [Fact]
    public void AssignedChildName_ReturnsChildFirstName()
    {
        // Arrange & Act
        var childName = _bot.AssignedChildName;

        // Assert
        Assert.Equal("Emma", childName);
    }

    [Fact]
    public async Task Start_WithMissingToken_LogsErrorAndReturns()
    {
        // Arrange
        var childWithoutToken = new Child
        {
            FirstName = "TestChild",
            Channels = new ChildChannels
            {
                Telegram = new ChildTelegramConfig
                {
                    Token = null // No token
                }
            }
        };
        var bot = new TelegramInteractiveBot(childWithoutToken, _mockAiService.Object, _mockLoggerFactory.Object);

        // Act
        await bot.Start();

        // Assert
        VerifyLoggerCall(LogLevel.Error, "Cannot start Telegram bot for TestChild: Token is missing");
    }

    [Fact]
    public async Task Start_WithEmptyToken_LogsErrorAndReturns()
    {
        // Arrange
        var childWithEmptyToken = new Child
        {
            FirstName = "TestChild",
            Channels = new ChildChannels
            {
                Telegram = new ChildTelegramConfig
                {
                    Token = "" // Empty token
                }
            }
        };
        var bot = new TelegramInteractiveBot(childWithEmptyToken, _mockAiService.Object, _mockLoggerFactory.Object);

        // Act
        await bot.Start();

        // Assert
        VerifyLoggerCall(LogLevel.Error, "Cannot start Telegram bot for TestChild: Token is missing");
    }

    [Fact]
    public async Task Start_WithNullChannels_LogsErrorAndReturns()
    {
        // Arrange
        var childWithoutChannels = new Child
        {
            FirstName = "TestChild",
            Channels = null // No channels configured
        };
        var bot = new TelegramInteractiveBot(childWithoutChannels, _mockAiService.Object, _mockLoggerFactory.Object);

        // Act
        await bot.Start();

        // Assert
        VerifyLoggerCall(LogLevel.Error, "Cannot start Telegram bot for TestChild: Token is missing");
    }

    [Fact]
    public async Task Start_WithNullTelegramConfig_LogsErrorAndReturns()
    {
        // Arrange
        var childWithoutTelegram = new Child
        {
            FirstName = "TestChild",
            Channels = new ChildChannels
            {
                Telegram = null // No Telegram config
            }
        };
        var bot = new TelegramInteractiveBot(childWithoutTelegram, _mockAiService.Object, _mockLoggerFactory.Object);

        // Act
        await bot.Start();

        // Assert
        VerifyLoggerCall(LogLevel.Error, "Cannot start Telegram bot for TestChild: Token is missing");
    }

    [Fact]
    public void Stop_CallsStopCorrectly()
    {
        // Act
        _bot.Stop();

        // Assert
        VerifyLoggerCall(LogLevel.Information, "Telegram bot stopped for Emma");
    }

    [Fact]
    public async Task SendMessageToTelegram_WithNullBotClient_LogsWarningAndReturns()
    {
        // Arrange
        var message = "Test message";

        // Act
        await _bot.SendMessageToTelegram(message);

        // Assert
        VerifyLoggerCall(LogLevel.Warning, "Cannot send message: Telegram bot not initialized");
    }

    [Fact]
    public async Task SendMessageToTelegram_WithNullChatId_LogsWarningAndReturns()
    {
        // Arrange
        var childWithoutChatId = new Child
        {
            FirstName = "TestChild",
            Channels = new ChildChannels
            {
                Telegram = new ChildTelegramConfig
                {
                    Token = "test-token",
                    ChatId = null // No chat ID
                }
            }
        };
        var bot = new TelegramInteractiveBot(childWithoutChatId, _mockAiService.Object, _mockLoggerFactory.Object);
        var message = "Test message";

        // Act
        await bot.SendMessageToTelegram(message);

        // Assert - Bot client is never initialized, so it logs the bot not initialized message
        VerifyLoggerCall(LogLevel.Warning, "Cannot send message: Telegram bot not initialized");
    }

    [Fact]
    public async Task SendMessageToTelegram_WithNullChannels_LogsWarningAndReturns()
    {
        // Arrange
        var childWithoutChannels = new Child
        {
            FirstName = "TestChild",
            Channels = null // No channels
        };
        var bot = new TelegramInteractiveBot(childWithoutChannels, _mockAiService.Object, _mockLoggerFactory.Object);
        var message = "Test message";

        // Act
        await bot.SendMessageToTelegram(message);

        // Assert - Bot client is never initialized, so it logs the bot not initialized message
        VerifyLoggerCall(LogLevel.Warning, "Cannot send message: Telegram bot not initialized");
    }

    [Theory]
    [InlineData("help")]
    [InlineData("HELP")]
    [InlineData("Help")]
    [InlineData("--help")]
    [InlineData("?")]
    [InlineData("commands")]
    [InlineData("/help")]
    [InlineData("/start")]
    [InlineData("hjælp")]
    [InlineData("kommandoer")]
    [InlineData("/hjælp")]
    [InlineData("  help  ")] // With spaces
    public void IsHelpCommand_WithValidHelpCommands_ReturnsTrue(string command)
    {
        // This tests the static IsHelpCommand method indirectly through reflection
        // since it's private, we test its behavior through the HandleUpdateAsync method

        // Arrange & Act & Assert
        var type = typeof(TelegramInteractiveBot);
        var method = type.GetMethod("IsHelpCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Invoke the private static method
        bool result = (bool)method!.Invoke(null, new object[] { command })!;

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("how are you")]
    [InlineData("what's the weather")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsHelpCommand_WithNonHelpCommands_ReturnsFalse(string command)
    {
        // This tests the static IsHelpCommand method through reflection

        // Arrange & Act
        var type = typeof(TelegramInteractiveBot);
        var method = type.GetMethod("IsHelpCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Invoke the private static method
        bool result = (bool)method!.Invoke(null, new object[] { command })!;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Dispose_CallsDisposeCorrectly()
    {
        // Arrange & Act
        _bot.Dispose();

        // Second call should not cause issues
        _bot.Dispose();

        // Assert - No exceptions thrown
        Assert.True(true);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        // Arrange & Act
        _bot.Dispose();
        _bot.Dispose();
        _bot.Dispose();

        // Assert - Multiple calls should not cause issues
        Assert.True(true);
    }

    [Theory]
    [InlineData("What activities does Emma have this week?")]
    [InlineData("Show me this week's letter")]
    [InlineData("What homework is there?")]
    [InlineData("Hvad skal Emma lave i dag?")]
    public async Task SendMessageToTelegram_WithValidMessage_ProcessesCorrectly(string message)
    {
        // This test verifies that the method can handle various message types
        // without throwing exceptions (since we can't easily test the full Telegram flow)

        // Act
        await _bot.SendMessageToTelegram(message);

        // Assert - Should log warning about bot not initialized
        VerifyLoggerCall(LogLevel.Warning, "Cannot send message: Telegram bot not initialized");
    }

    [Fact]
    public void Constructor_SetsChildNameCorrectly()
    {
        // Arrange
        var child = new Child
        {
            FirstName = "TestChildName",
            LastName = "LastName"
        };

        // Act
        var bot = new TelegramInteractiveBot(child, _mockAiService.Object, _mockLoggerFactory.Object);

        // Assert
        Assert.Equal("TestChildName", bot.AssignedChildName);
    }

    [Fact]
    public async Task SendMessageToTelegram_WithLongMessage_ProcessesCorrectly()
    {
        // Arrange
        var longMessage = new string('x', 1000); // 1000 character message

        // Act
        await _bot.SendMessageToTelegram(longMessage);

        // Assert - Should handle long messages without issues
        VerifyLoggerCall(LogLevel.Warning, "Cannot send message: Telegram bot not initialized");
    }

    [Fact]
    public async Task SendMessageToTelegram_WithSpecialCharacters_ProcessesCorrectly()
    {
        // Arrange
        var messageWithSpecialChars = "Test: <b>Bold</b> & <i>Italic</i> text with émojis 🤖";

        // Act
        await _bot.SendMessageToTelegram(messageWithSpecialChars);

        // Assert - Should handle special characters without issues
        VerifyLoggerCall(LogLevel.Warning, "Cannot send message: Telegram bot not initialized");
    }

    [Fact]
    public async Task SendMessageToTelegram_WithEmptyMessage_ProcessesCorrectly()
    {
        // Arrange
        var emptyMessage = "";

        // Act
        await _bot.SendMessageToTelegram(emptyMessage);

        // Assert - Should handle empty message without issues
        VerifyLoggerCall(LogLevel.Warning, "Cannot send message: Telegram bot not initialized");
    }

    [Fact]
    public async Task SendMessageToTelegram_WithNullMessage_ProcessesCorrectly()
    {
        // Arrange
        string nullMessage = null!;

        // Act
        await _bot.SendMessageToTelegram(nullMessage);

        // Assert - Should handle null message without issues
        VerifyLoggerCall(LogLevel.Warning, "Cannot send message: Telegram bot not initialized");
    }

    [Theory]
    [InlineData(12345)]
    [InlineData(-67890)]
    [InlineData(0)]
    public void Constructor_WithDifferentChatIds_InitializesCorrectly(long chatId)
    {
        // Arrange
        var child = new Child
        {
            FirstName = "TestChild",
            Channels = new ChildChannels
            {
                Telegram = new ChildTelegramConfig
                {
                    Token = "test-token",
                    ChatId = chatId
                }
            }
        };

        // Act
        var bot = new TelegramInteractiveBot(child, _mockAiService.Object, _mockLoggerFactory.Object);

        // Assert
        Assert.NotNull(bot);
        Assert.Equal("TestChild", bot.AssignedChildName);
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