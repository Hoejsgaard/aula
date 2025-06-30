using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Aula.Bots;
using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using Aula.Tools;
using Aula.Utilities;

namespace Aula.Tests.Bots;

public class SlackMessageHandlerTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly Mock<ILogger> _mockLogger;
    private readonly HttpClient _httpClient;
    private readonly ReminderCommandHandler _reminderHandler;
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Config _testConfig;
    private readonly Dictionary<string, Child> _childrenByName;
    private readonly ConversationContext _conversationContext;
    private readonly SlackMessageHandler _messageHandler;

    public SlackMessageHandlerTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _mockLogger = new Mock<ILogger>();
        _httpClient = new HttpClient();
        _mockSupabaseService = new Mock<ISupabaseService>();

        _testConfig = new Config
        {
            Slack = new Aula.Configuration.Slack
            {
                ApiToken = "test-token",
                ChannelId = "test-channel"
            },
            Children = new List<Child>
            {
                new Child { FirstName = "Emma", LastName = "Test" },
                new Child { FirstName = "Hans", LastName = "Test" }
            }
        };

        _childrenByName = new Dictionary<string, Child>
        {
            { "emma", _testConfig.Children[0] },
            { "hans", _testConfig.Children[1] }
        };

        _conversationContext = new ConversationContext();
        _reminderHandler = new ReminderCommandHandler(_mockLogger.Object, _mockSupabaseService.Object, _childrenByName);

        _messageHandler = new SlackMessageHandler(
            _mockAgentService.Object,
            _testConfig,
            _mockLogger.Object,
            _httpClient,
            _childrenByName,
            _conversationContext,
            _reminderHandler);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        Assert.NotNull(_messageHandler);
    }

    [Fact]
    public void Constructor_WithNullAgentService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackMessageHandler(
                null!,
                _testConfig,
                _mockLogger.Object,
                _httpClient,
                _childrenByName,
                _conversationContext,
                _reminderHandler));
        Assert.Equal("agentService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackMessageHandler(
                _mockAgentService.Object,
                null!,
                _mockLogger.Object,
                _httpClient,
                _childrenByName,
                _conversationContext,
                _reminderHandler));
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackMessageHandler(
                _mockAgentService.Object,
                _testConfig,
                null!,
                _httpClient,
                _childrenByName,
                _conversationContext,
                _reminderHandler));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackMessageHandler(
                _mockAgentService.Object,
                _testConfig,
                _mockLogger.Object,
                null!,
                _childrenByName,
                _conversationContext,
                _reminderHandler));
        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullChildrenByName_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackMessageHandler(
                _mockAgentService.Object,
                _testConfig,
                _mockLogger.Object,
                _httpClient,
                null!,
                _conversationContext,
                _reminderHandler));
        Assert.Equal("childrenByName", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullConversationContext_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackMessageHandler(
                _mockAgentService.Object,
                _testConfig,
                _mockLogger.Object,
                _httpClient,
                _childrenByName,
                null!,
                _reminderHandler));
        Assert.Equal("conversationContext", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullReminderHandler_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackMessageHandler(
                _mockAgentService.Object,
                _testConfig,
                _mockLogger.Object,
                _httpClient,
                _childrenByName,
                _conversationContext,
                null!));
        Assert.Equal("reminderHandler", exception.ParamName);
    }

    [Fact]
    public async Task HandleMessageAsync_WithValidMessage_ReturnsTrue()
    {
        var eventData = new JObject
        {
            ["channel"] = "test-channel",
            ["text"] = "Hello bot",
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        var result = await _messageHandler.HandleMessageAsync(eventData);

        Assert.True(result);
    }

    [Fact]
    public async Task HandleMessageAsync_WithEmptyText_ReturnsFalse()
    {
        var eventData = new JObject
        {
            ["channel"] = "test-channel",
            ["text"] = "",
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        var result = await _messageHandler.HandleMessageAsync(eventData);

        Assert.False(result);
    }

    [Fact]
    public async Task HandleMessageAsync_WithNullText_ReturnsFalse()
    {
        var eventData = new JObject
        {
            ["channel"] = "test-channel",
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        var result = await _messageHandler.HandleMessageAsync(eventData);

        Assert.False(result);
    }

    [Fact]
    public async Task HandleMessageAsync_WithEmptyChannel_ReturnsFalse()
    {
        var eventData = new JObject
        {
            ["channel"] = "",
            ["text"] = "Hello bot",
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        var result = await _messageHandler.HandleMessageAsync(eventData);

        Assert.False(result);
    }

    [Fact]
    public async Task HandleMessageAsync_WithNullChannel_ReturnsFalse()
    {
        var eventData = new JObject
        {
            ["text"] = "Hello bot",
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        var result = await _messageHandler.HandleMessageAsync(eventData);

        Assert.False(result);
    }

    [Theory]
    [InlineData("hjælp")]
    [InlineData("help")]
    [InlineData("HJÆLP")]
    [InlineData("HELP")]
    public async Task HandleMessageAsync_WithHelpCommand_ProcessesHelpMessage(string helpCommand)
    {
        var eventData = new JObject
        {
            ["channel"] = "test-channel",
            ["text"] = helpCommand,
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        var result = await _messageHandler.HandleMessageAsync(eventData);

        Assert.True(result);
        
        // Verify logging occurred for help command processing
        _mockLogger.Verify(
            logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("mind mig om noget")]
    [InlineData("tilføj påmindelse")]
    [InlineData("slet påmindelse")]
    [InlineData("vis påmindelser")]
    public async Task HandleMessageAsync_WithReminderCommand_ProcessesMessage(string reminderCommand)
    {
        var eventData = new JObject
        {
            ["channel"] = "test-channel",
            ["text"] = reminderCommand,
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        var result = await _messageHandler.HandleMessageAsync(eventData);

        Assert.True(result);
    }

    [Fact]
    public async Task HandleMessageAsync_WithException_LogsErrorAndReturnsFalse()
    {
        _mockAgentService
            .Setup(a => a.ProcessQueryWithToolsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatInterface>()))
            .ThrowsAsync(new Exception("Test exception"));

        var eventData = new JObject
        {
            ["channel"] = "test-channel",
            ["text"] = "What is Emma doing today?",
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        var result = await _messageHandler.HandleMessageAsync(eventData);

        Assert.False(result);

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error processing Slack message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Hvad skal Emma i dag?")]
    [InlineData("What is Hans doing tomorrow?")]
    [InlineData("Vis ugeplanen")]
    public async Task HandleMessageAsync_WithValidQuery_ProcessesSuccessfully(string queryText)
    {
        _mockAgentService
            .Setup(a => a.GetWeekLetterAsync(It.IsAny<Child>(), It.IsAny<DateOnly>(), It.IsAny<bool>()))
            .ReturnsAsync(new JObject());

        var eventData = new JObject
        {
            ["channel"] = "test-channel",
            ["text"] = queryText,
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        var result = await _messageHandler.HandleMessageAsync(eventData);

        Assert.True(result);
    }

    [Fact]
    public async Task HandleMessageAsync_LogsMessageProcessing()
    {
        var eventData = new JObject
        {
            ["channel"] = "test-channel",
            ["text"] = "Test message",
            ["ts"] = "1234567890.123456",
            ["user"] = "test-user"
        };

        await _messageHandler.HandleMessageAsync(eventData);

        _mockLogger.Verify(
            logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing Slack message")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}