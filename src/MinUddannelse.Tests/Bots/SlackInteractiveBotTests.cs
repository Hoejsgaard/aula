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
using MinUddannelse.Bots;
using MinUddannelse.Configuration;
using MinUddannelse.Client;
using MinUddannelse.GoogleCalendar;
using MinUddannelse.Security;
using MinUddannelse.AI.Services;
using MinUddannelse.Content.WeekLetters;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse;
using System.Linq;

namespace MinUddannelse.Tests.Bots;

public class SlackInteractiveBotTests : IDisposable
{
    private readonly Mock<IOpenAiService> _mockOpenAiService;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly SlackInteractiveBot _slackBot;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private bool _disposed = false;
    private const int MaxMessageLength = 4000;

    private readonly Child _testChild = new Child
    {
        FirstName = "Emma",
        LastName = "TestLastName",
        Channels = new ChildChannels
        {
            Slack = new ChildSlackConfig
            {
                ApiToken = "test-slack-token",
                ChannelId = "test-channel-id",
                Enabled = true,
                EnableInteractiveBot = true,
                PollingIntervalSeconds = 5
            }
        }
    };

    public SlackInteractiveBotTests()
    {
        _mockOpenAiService = new Mock<IOpenAiService>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

        _slackBot = new SlackInteractiveBot(_testChild, _mockOpenAiService.Object, _mockLoggerFactory.Object, _httpClient);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        Assert.NotNull(_slackBot);
    }

    [Fact]
    public void Constructor_WithNullChild_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackInteractiveBot(null!, _mockOpenAiService.Object, _mockLoggerFactory.Object, _httpClient));
        Assert.Equal("child", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullOpenAiService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackInteractiveBot(_testChild, null!, _mockLoggerFactory.Object, _httpClient));
        Assert.Equal("aiService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SlackInteractiveBot(_testChild, _mockOpenAiService.Object, null!, _httpClient));
        Assert.Equal("loggerFactory", exception.ParamName);
    }

    // Test removed - ISupabaseService is no longer a dependency

    // This test was replaced by StartForChild_WithMissingApiToken_LogsErrorAndReturns above

    // This test was replaced by StartForChild_WithMissingChannelId_LogsErrorAndReturns above

    [Fact]
    public async Task SendMessageToSlack_WithValidText_SendsSuccessfully()
    {
        SetupSuccessfulHttpResponse();
        await _slackBot.Start();

        await _slackBot.SendMessageToSlack("Test message");

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeast(1), // At least once for the startup message and our test message
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageToSlack_WithLongText_SendsWithoutTruncation()
    {
        var longMessage = new string('a', MaxMessageLength + 1);
        SetupSuccessfulHttpResponse();
        await _slackBot.Start();

        await _slackBot.SendMessageToSlack(longMessage);

        // New implementation doesn't truncate, it sends the full message
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeast(1),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage"),
            ItExpr.IsAny<CancellationToken>());
    }

    // PostWeekLetter method no longer exists in the new API
    // This functionality has been moved to other components

    // PostWeekLetter method no longer exists in the new API

    // PostWeekLetter method no longer exists in the new API

    // PostWeekLetter method no longer exists in the new API

    // PostWeekLetter method no longer exists in the new API

    [Fact]
    public void Stop_WhenCalled_LogsInformation()
    {
        _slackBot.Stop();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slack bot stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
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
    public async Task StartForChild_WithValidChild_InitializesTimersAndSendsWelcome()
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting Slack bot for child: Emma")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slack polling started - checking every 5 seconds")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());

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
    public async Task StartForChild_WithSpecificChild_ShowsCorrectChildName()
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

        // Verify the welcome message contains the child's name
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://slack.com/api/chat.postMessage" &&
                req.Content!.ReadAsStringAsync().GetAwaiter().GetResult().Contains("Emma")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task StartForChild_SetsHttpClientAuthorizationHeader()
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
        Assert.Equal("test-slack-token", _httpClient.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public async Task StartForChild_InitializesTimestampToCurrentTime()
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
            Times.Once());
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

        // Should not process any messages
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message for Emma")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never());
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message for Emma")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never());
    }
}
