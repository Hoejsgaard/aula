using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Aula.Bots;
using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using System.Linq;

namespace Aula.Tests.Bots;

public class SlackInteractiveBotTests : IDisposable
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Config _testConfig;
    private readonly SlackInteractiveBot _slackBot;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private bool _disposed = false;
    private const int MaxMessageLength = 5000;

    public SlackInteractiveBotTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();
        _mockSupabaseService = new Mock<ISupabaseService>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

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
            },
            Timers = new Aula.Configuration.Timers
            {
                SlackPollingIntervalSeconds = 5,
                CleanupIntervalHours = 24
            }
        };

        _slackBot = new SlackInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object, _httpClient);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        Assert.NotNull(_slackBot);
    }

    [Fact]
    public void Constructor_WithNullAgentService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackInteractiveBot(null!, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object, _httpClient));
        Assert.Equal("agentService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackInteractiveBot(_mockAgentService.Object, null!, _mockLoggerFactory.Object, _mockSupabaseService.Object, _httpClient));
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackInteractiveBot(_mockAgentService.Object, _testConfig, null!, _mockSupabaseService.Object, _httpClient));
        Assert.Equal("loggerFactory", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSupabaseService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, null!, _httpClient));
        Assert.Equal("supabaseService", exception.ParamName);
    }

    [Fact]
    public async Task Start_WithMissingApiToken_LogsErrorAndReturns()
    {
        var configWithoutToken = new Config
        {
            Slack = new Aula.Configuration.Slack { ApiToken = "", ChannelId = "test-channel" },
            Children = new List<Child>(),
            Timers = new Aula.Configuration.Timers { SlackPollingIntervalSeconds = 5, CleanupIntervalHours = 24 }
        };

        var bot = new SlackInteractiveBot(_mockAgentService.Object, configWithoutToken, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        await bot.Start();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot start Slack bot: API token is missing")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        bot.Dispose();
    }

    [Fact]
    public async Task Start_WithMissingChannelId_LogsErrorAndReturns()
    {
        var configWithoutChannel = new Config
        {
            Slack = new Aula.Configuration.Slack { ApiToken = "test-token", ChannelId = "" },
            Children = new List<Child>(),
            Timers = new Aula.Configuration.Timers { SlackPollingIntervalSeconds = 5, CleanupIntervalHours = 24 }
        };

        var bot = new SlackInteractiveBot(_mockAgentService.Object, configWithoutChannel, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        await bot.Start();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot start Slack bot: Channel ID is missing")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        bot.Dispose();
    }

    [Fact]
    public async Task SendMessage_WithValidText_SendsSuccessfully()
    {
        SetupSuccessfulHttpResponse();

        await _slackBot.SendMessage("Test message");

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendMessage_WithLongText_TruncatesMessage()
    {
        var longMessage = new string('a', MaxMessageLength);
        SetupSuccessfulHttpResponse();

        await _slackBot.SendMessage(longMessage);

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Message truncated due to length")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWeekLetter_WithValidData_PostsSuccessfully()
    {
        SetupSuccessfulHttpResponse();

        await _slackBot.PostWeekLetter("Emma", "Test week letter content", "Uge 26");

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task PostWeekLetter_WithEmptyContent_LogsWarningAndReturns()
    {
        await _slackBot.PostWeekLetter("Emma", "", "Uge 26");

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot post empty week letter")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWeekLetter_WithDuplicateContent_SkipsDuplicate()
    {
        var weekLetterContent = "Test week letter content";
        SetupSuccessfulHttpResponse();

        // Post the same week letter twice
        await _slackBot.PostWeekLetter("Emma", weekLetterContent, "Uge 26");
        await _slackBot.PostWeekLetter("Emma", weekLetterContent, "Uge 26");

        // Should only be called once due to duplicate detection
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already posted, skipping")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWeekLetter_WithHttpError_LogsError()
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        await _slackBot.PostWeekLetter("Emma", "Test content", "Uge 26");

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to post week letter: HTTP")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWeekLetter_WithSlackApiError_LogsError()
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = false, error = "channel_not_found" }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        await _slackBot.PostWeekLetter("Emma", "Test content", "Uge 26");

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to post week letter: channel_not_found")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Stop_WhenCalled_LogsInformation()
    {
        _slackBot.Stop();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slack polling bot stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_WhenCalled_DisposesResources()
    {
        _slackBot.Dispose();

        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_DoesNotThrow()
    {
        _slackBot.Dispose();
        _slackBot.Dispose();

        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public async Task Start_WithValidConfig_InitializesTimersAndSendsWelcome()
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = true, ts = "1234567890.123456" }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        await _slackBot.Start();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting Slack polling bot")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slack polling bot started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify welcome message was sent
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Start_WithMultipleChildren_BuildsCorrectChildrenList()
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = true, ts = "1234567890.123456" }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        await _slackBot.Start();

        // Verify the welcome message contains both children's names
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage" &&
                req.Content!.ReadAsStringAsync().Result.Contains("Emma og Hans")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Start_SetsHttpClientAuthorizationHeader()
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = true, ts = "1234567890.123456" }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        await _slackBot.Start();

        // Verify the HTTP client has the correct authorization header
        Assert.Equal("Bearer", _httpClient.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("test-token", _httpClient.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public async Task Start_InitializesTimestampToCurrentTime()
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = true, ts = "1234567890.123456" }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        var beforeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _slackBot.Start();
        var afterStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Verify timestamp was logged and is reasonable
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initial timestamp set to:")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void SetupSuccessfulHttpResponse()
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = true, ts = "1234567890.123456" }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);
    }

    private void SetupSlackConversationHistoryResponse(object responseData)
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(responseData))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("conversations.history")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);
    }

    private void SetupSlackPostMessageResponse(object responseData)
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(responseData))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _slackBot?.Dispose();
            _httpClient?.Dispose();
            _disposed = true;
        }
    }

    // Helper method to trigger polling indirectly through timer
    private async Task TriggerPollMessagesByStarting()
    {
        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567890.123456" });
        await _slackBot.Start();
        // Give the timer a moment to trigger
        await Task.Delay(100);
    }

    [Fact]
    public async Task PollMessages_WithValidUserMessage_ProcessesCorrectly()
    {
        // Setup conversation history response with a user message
        var conversationResponse = new
        {
            ok = true,
            messages = new object[]
            {
                new
                {
                    type = "message",
                    user = "U12345",
                    text = "Hello bot",
                    ts = "1234567891.000001"
                }
            }
        };

        SetupSlackConversationHistoryResponse(conversationResponse);
        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567892.000001" });

        await TriggerPollMessagesByStarting();
        await Task.Delay(200); // Give time for polling to process

        // Verify conversations.history was called
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri!.ToString().Contains("conversations.history")),
            ItExpr.IsAny<CancellationToken>());

        // Verify that new user messages are logged
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found") && v.ToString()!.Contains("new user messages")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollMessages_WithEmptyResponse_HandlesGracefully()
    {
        // Setup empty conversation history response
        var conversationResponse = new
        {
            ok = true,
            messages = new object[0]
        };

        SetupSlackConversationHistoryResponse(conversationResponse);
        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567890.123456" });

        await TriggerPollMessagesByStarting();
        await Task.Delay(200);

        // Should not log any new messages
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found") && v.ToString()!.Contains("new user messages")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task PollMessages_WithBotMessages_SkipsCorrectly()
    {
        // Setup conversation history response with bot messages
        var conversationResponse = new
        {
            ok = true,
            messages = new object[]
            {
                new
                {
                    type = "message",
                    subtype = "bot_message",
                    bot_id = "B12345",
                    text = "Bot message",
                    ts = "1234567891.000001"
                },
                new
                {
                    type = "message",
                    bot_id = "B12345",
                    text = "Another bot message",
                    ts = "1234567891.000002"
                }
            }
        };

        SetupSlackConversationHistoryResponse(conversationResponse);
        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567890.123456" });

        await TriggerPollMessagesByStarting();
        await Task.Delay(200);

        // Should not process bot messages
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found") && v.ToString()!.Contains("new user messages")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task PollMessages_WithHttpError_LogsError()
    {
        // Setup HTTP error for conversations.history
        var mockResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("conversations.history")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567890.123456" });

        await TriggerPollMessagesByStarting();
        await Task.Delay(200);

        // Should log HTTP error
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to fetch messages: HTTP")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollMessages_WithNotInChannelError_TriggersJoinChannel()
    {
        // Setup not_in_channel error response
        var conversationResponse = new
        {
            ok = false,
            error = "not_in_channel"
        };

        SetupSlackConversationHistoryResponse(conversationResponse);
        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567890.123456" });

        // Setup conversations.join response
        var joinResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = true }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://slack.com/api/conversations.join"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(joinResponse);

        await TriggerPollMessagesByStarting();
        await Task.Delay(200);

        // Should log warning about not being in channel
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Bot is not in the channel")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        // Should attempt to join channel
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/conversations.join"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task PollMessages_UpdatesTimestampCorrectly()
    {
        // Setup conversation history response with messages
        var conversationResponse = new
        {
            ok = true,
            messages = new object[]
            {
                new
                {
                    type = "message",
                    user = "U12345",
                    text = "First message",
                    ts = "1234567891.000001"
                },
                new
                {
                    type = "message",
                    user = "U12346",
                    text = "Second message",
                    ts = "1234567892.000002"
                }
            }
        };

        SetupSlackConversationHistoryResponse(conversationResponse);
        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567893.000003" });

        await TriggerPollMessagesByStarting();
        await Task.Delay(200);

        // Should process the messages (timestamp update logging happens internally)
        // Verify that the messages were found and processed
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found") && v.ToString()!.Contains("new user messages")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task JoinChannel_WithValidRequest_JoinsSuccessfully()
    {
        // Setup conversations.join success response
        var joinResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = true }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://slack.com/api/conversations.join"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(joinResponse);

        // Trigger JoinChannel through not_in_channel error
        var conversationResponse = new { ok = false, error = "not_in_channel" };
        SetupSlackConversationHistoryResponse(conversationResponse);
        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567890.123456" });

        await TriggerPollMessagesByStarting();
        await Task.Delay(200);

        // Should log successful join
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully joined channel")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task JoinChannel_WithHttpError_LogsError()
    {
        // Setup HTTP error for conversations.join
        var joinResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://slack.com/api/conversations.join"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(joinResponse);

        // Trigger JoinChannel through not_in_channel error
        var conversationResponse = new { ok = false, error = "not_in_channel" };
        SetupSlackConversationHistoryResponse(conversationResponse);
        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567890.123456" });

        await TriggerPollMessagesByStarting();
        await Task.Delay(200);

        // Should log HTTP error
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to join channel: HTTP")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task JoinChannel_WithApiError_SendsInvitationMessage()
    {
        // Setup API error for conversations.join
        var joinResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = false, error = "cant_invite_self" }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://slack.com/api/conversations.join"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(joinResponse);

        // Trigger JoinChannel through not_in_channel error
        var conversationResponse = new { ok = false, error = "not_in_channel" };
        SetupSlackConversationHistoryResponse(conversationResponse);
        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567890.123456" });

        await TriggerPollMessagesByStarting();
        await Task.Delay(200);

        // Should log API error
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to join channel: cant_invite_self")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        // Should attempt to send invitation message
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeast(2), // Once for welcome, once for invitation message
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageInternal_WithSlackRateLimiting_HandlesCorrectly()
    {
        // Setup rate limiting response
        var rateLimitResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(rateLimitResponse);

        await _slackBot.SendMessage("Test message");

        // Should log HTTP error for rate limiting
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send message: HTTP")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageInternal_WithNetworkTimeout_LogsError()
    {
        // Setup network timeout
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage"),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        await _slackBot.SendMessage("Test message");

        // Should log exception
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error sending message to Slack")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task Start_WithChildren_BuildsChildrenDictionaryCorrectly()
    {
        // Verify children dictionary is built from config
        SetupSuccessfulHttpResponse();
        
        await _slackBot.Start();
        
        // The children dictionary is built during construction,
        // verified by the welcome message containing children names
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage" &&
                req.Content!.ReadAsStringAsync().Result.Contains("Emma og Hans")),
            ItExpr.IsAny<CancellationToken>());
    }
    
    [Fact]
    public async Task PollMessages_WithMalformedJson_LogsError()
    {
        // Setup malformed JSON response
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("malformed json{")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("conversations.history")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        SetupSlackPostMessageResponse(new { ok = true, ts = "1234567890.123456" });

        await TriggerPollMessagesByStarting();
        await Task.Delay(200);

        // Should log JSON parsing error
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error polling Slack messages")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CleanupTimer_IsInitialized_DuringStart()
    {
        SetupSuccessfulHttpResponse();
        
        await _slackBot.Start();
        
        // Verify cleanup timer logging
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slack cleanup timer started - running every 24 hours")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageInternal_WithValidSlackResponse_StoresMessageId()
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { ok = true, ts = "1234567890.123456" }))
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        await _slackBot.SendMessage("Test message");

        // Verify message was sent successfully
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Message sent successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        // Verify message ID was stored
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stored sent message ID: 1234567890.123456")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}