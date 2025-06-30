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
                new Child { FirstName = TestChild1, LastName = "Test" }
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
        var longMessage = new string('a', 5000);
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _slackBot?.Dispose();
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}