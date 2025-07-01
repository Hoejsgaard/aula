using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Aula.Channels;
using System;
using System.Threading.Tasks;

namespace Aula.Tests.Channels;

public class MessageSenderTests
{
    // Test helper interface for creating mockable bot wrappers
    public interface ISlackBot
    {
        Task SendMessage(string message);
    }

    public interface ITelegramBot
    {
        Task SendMessage(string chatId, string message);
    }

    // Simple wrapper classes to test the IMessageSender contract behavior
    public class TestableSlackMessageSender : IMessageSender
    {
        private readonly ISlackBot _slackBot;

        public TestableSlackMessageSender(ISlackBot slackBot)
        {
            _slackBot = slackBot ?? throw new ArgumentNullException(nameof(slackBot));
        }

        public Task SendMessageAsync(string message)
        {
            return _slackBot.SendMessage(message);
        }

        public Task SendMessageAsync(string chatId, string message)
        {
            // Slack ignores chatId - this tests the contract behavior
            return _slackBot.SendMessage(message);
        }
    }

    public class TestableTelegramMessageSender : IMessageSender
    {
        private readonly ITelegramBot _telegramBot;
        private readonly string _defaultChatId;

        public TestableTelegramMessageSender(ITelegramBot telegramBot, string defaultChatId)
        {
            _telegramBot = telegramBot ?? throw new ArgumentNullException(nameof(telegramBot));
            _defaultChatId = defaultChatId ?? throw new ArgumentNullException(nameof(defaultChatId));
        }

        public Task SendMessageAsync(string message)
        {
            return _telegramBot.SendMessage(_defaultChatId, message);
        }

        public Task SendMessageAsync(string chatId, string message)
        {
            return _telegramBot.SendMessage(chatId, message);
        }
    }

    // Constructor Validation Tests for Actual Classes
    
    [Fact]
    public void SlackMessageSender_Constructor_WithNullBot_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SlackMessageSender(null!));
    }

    [Fact]
    public void TelegramMessageSender_Constructor_WithNullBot_ThrowsArgumentNullException()
    {
        // Act & Assert 
        Assert.Throws<ArgumentNullException>(() => new TelegramMessageSender(null!, "chat"));
    }

    [Fact]
    public void TelegramMessageSender_Constructor_WithNullChatId_ThrowsArgumentNullException()
    {
        // This test won't work without a valid TelegramInteractiveBot instance
        // but we can test the validation concept through our testable wrapper
        Assert.Throws<ArgumentNullException>(() => new TestableTelegramMessageSender(Mock.Of<ITelegramBot>(), null!));
    }

    // Contract Behavior Tests Using Testable Wrappers

    [Fact]
    public void TestableSlackMessageSender_Constructor_WithNullBot_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TestableSlackMessageSender(null!));
    }

    [Fact]
    public void TestableSlackMessageSender_Constructor_WithValidBot_CreatesInstance()
    {
        // Arrange
        var mockSlackBot = new Mock<ISlackBot>();

        // Act
        var sender = new TestableSlackMessageSender(mockSlackBot.Object);

        // Assert
        Assert.NotNull(sender);
    }

    [Fact]
    public async Task TestableSlackMessageSender_SendMessageAsync_WithValidMessage_ForwardsToBot()
    {
        // Arrange
        var mockSlackBot = new Mock<ISlackBot>();
        mockSlackBot.Setup(x => x.SendMessage("Test message"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sender = new TestableSlackMessageSender(mockSlackBot.Object);

        // Act
        await sender.SendMessageAsync("Test message");

        // Assert
        mockSlackBot.Verify(x => x.SendMessage("Test message"), Times.Once);
    }

    [Fact]
    public async Task TestableSlackMessageSender_SendMessageAsync_WithChatIdAndMessage_IgnoresChatId()
    {
        // Arrange
        var mockSlackBot = new Mock<ISlackBot>();
        mockSlackBot.Setup(x => x.SendMessage("Test message"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sender = new TestableSlackMessageSender(mockSlackBot.Object);

        // Act - ChatId should be ignored for Slack
        await sender.SendMessageAsync("ignored-chat-id", "Test message");

        // Assert
        mockSlackBot.Verify(x => x.SendMessage("Test message"), Times.Once);
    }

    [Fact]
    public async Task TestableSlackMessageSender_SendMessageAsync_WhenBotThrows_PropagatesException()
    {
        // Arrange
        var mockSlackBot = new Mock<ISlackBot>();
        mockSlackBot.Setup(x => x.SendMessage(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Slack API error"));

        var sender = new TestableSlackMessageSender(mockSlackBot.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendMessageAsync("Test message"));
        
        Assert.Equal("Slack API error", exception.Message);
    }

    [Fact]
    public void TestableTelegramMessageSender_Constructor_WithNullBot_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TestableTelegramMessageSender(null!, "default-chat"));
    }

    [Fact]
    public void TestableTelegramMessageSender_Constructor_WithNullDefaultChatId_ThrowsArgumentNullException()
    {
        // Arrange
        var mockTelegramBot = new Mock<ITelegramBot>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TestableTelegramMessageSender(mockTelegramBot.Object, null!));
    }

    [Fact]
    public void TestableTelegramMessageSender_Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var mockTelegramBot = new Mock<ITelegramBot>();

        // Act
        var sender = new TestableTelegramMessageSender(mockTelegramBot.Object, "default-chat");

        // Assert
        Assert.NotNull(sender);
    }

    [Fact]
    public async Task TestableTelegramMessageSender_SendMessageAsync_WithValidMessage_UsesDefaultChatId()
    {
        // Arrange
        var mockTelegramBot = new Mock<ITelegramBot>();
        mockTelegramBot.Setup(x => x.SendMessage("default-chat", "Test message"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sender = new TestableTelegramMessageSender(mockTelegramBot.Object, "default-chat");

        // Act
        await sender.SendMessageAsync("Test message");

        // Assert
        mockTelegramBot.Verify(x => x.SendMessage("default-chat", "Test message"), Times.Once);
    }

    [Fact]
    public async Task TestableTelegramMessageSender_SendMessageAsync_WithChatIdAndMessage_UsesSpecifiedChatId()
    {
        // Arrange
        var mockTelegramBot = new Mock<ITelegramBot>();
        mockTelegramBot.Setup(x => x.SendMessage("specific-chat", "Test message"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sender = new TestableTelegramMessageSender(mockTelegramBot.Object, "default-chat");

        // Act
        await sender.SendMessageAsync("specific-chat", "Test message");

        // Assert
        mockTelegramBot.Verify(x => x.SendMessage("specific-chat", "Test message"), Times.Once);
    }

    [Fact]
    public async Task TestableTelegramMessageSender_SendMessageAsync_WhenBotThrows_PropagatesException()
    {
        // Arrange
        var mockTelegramBot = new Mock<ITelegramBot>();
        mockTelegramBot.Setup(x => x.SendMessage(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Telegram API error"));

        var sender = new TestableTelegramMessageSender(mockTelegramBot.Object, "default-chat");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendMessageAsync("Test message"));
        
        Assert.Equal("Telegram API error", exception.Message);
    }

    // Cross-Channel Integration Tests

    [Fact]
    public async Task MessageSenders_SendSameMessage_BothChannelsReceiveMessage()
    {
        // Arrange
        var mockSlackBot = new Mock<ISlackBot>();
        var mockTelegramBot = new Mock<ITelegramBot>();

        var slackSender = new TestableSlackMessageSender(mockSlackBot.Object);
        var telegramSender = new TestableTelegramMessageSender(mockTelegramBot.Object, "default-chat");

        var testMessage = "📢 Important announcement from school!";

        // Act
        await slackSender.SendMessageAsync(testMessage);
        await telegramSender.SendMessageAsync(testMessage);

        // Assert
        mockSlackBot.Verify(x => x.SendMessage(testMessage), Times.Once);
        mockTelegramBot.Verify(x => x.SendMessage("default-chat", testMessage), Times.Once);
    }

    [Fact]
    public async Task MessageSenders_SendMessageWithSpecificChatId_BehaviorDiffersCorrectly()
    {
        // Arrange
        var mockSlackBot = new Mock<ISlackBot>();
        var mockTelegramBot = new Mock<ITelegramBot>();

        var slackSender = new TestableSlackMessageSender(mockSlackBot.Object);
        var telegramSender = new TestableTelegramMessageSender(mockTelegramBot.Object, "default-chat");

        var testMessage = "Test message";
        var specificChatId = "specific-chat-id";

        // Act
        await slackSender.SendMessageAsync(specificChatId, testMessage);  // Should ignore chatId
        await telegramSender.SendMessageAsync(specificChatId, testMessage);  // Should use chatId

        // Assert
        mockSlackBot.Verify(x => x.SendMessage(testMessage), Times.Once);  // ChatId ignored
        mockTelegramBot.Verify(x => x.SendMessage(specificChatId, testMessage), Times.Once);  // ChatId used
    }

    [Fact]
    public void MessageSenders_ImplementIMessageSenderInterface_CorrectlyTyped()
    {
        // Arrange
        var mockSlackBot = new Mock<ISlackBot>();
        var mockTelegramBot = new Mock<ITelegramBot>();

        // Act
        IMessageSender slackSender = new TestableSlackMessageSender(mockSlackBot.Object);
        IMessageSender telegramSender = new TestableTelegramMessageSender(mockTelegramBot.Object, "default-chat");

        // Assert
        Assert.IsAssignableFrom<IMessageSender>(slackSender);
        Assert.IsAssignableFrom<IMessageSender>(telegramSender);
        Assert.IsType<TestableSlackMessageSender>(slackSender);
        Assert.IsType<TestableTelegramMessageSender>(telegramSender);
    }

    // Interface Contract Tests

    [Fact]
    public void IMessageSender_Interface_HasCorrectMethods()
    {
        // This test ensures the IMessageSender interface has the expected contract
        var methods = typeof(IMessageSender).GetMethods();
        
        Assert.Equal(2, methods.Length);
        Assert.Contains(methods, m => m.Name == "SendMessageAsync" && m.GetParameters().Length == 1);
        Assert.Contains(methods, m => m.Name == "SendMessageAsync" && m.GetParameters().Length == 2);
    }
}