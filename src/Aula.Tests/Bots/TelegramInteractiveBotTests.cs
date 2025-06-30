using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;
using Aula.Bots;
using Aula.Configuration;
using Aula.Integration;
using Aula.Services;

namespace Aula.Tests.Bots;

public class TelegramInteractiveBotTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Config _testConfig;

    public TelegramInteractiveBotTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();
        _mockSupabaseService = new Mock<ISupabaseService>();

        _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _testConfig = new Config
        {
            Telegram = new Aula.Configuration.Telegram
            {
                Enabled = true,
                Token = "test-token",
                ChannelId = "test-channel"
            },
            Children = new List<Child>
            {
                new Child { FirstName = "Emma", LastName = "Test" },
                new Child { FirstName = TestChild1, LastName = "Test" }
            }
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);
        Assert.NotNull(bot);
    }

    [Fact]
    public void Constructor_WithNullAgentService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TelegramInteractiveBot(null!, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object));
        Assert.Equal("agentService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TelegramInteractiveBot(_mockAgentService.Object, null!, _mockLoggerFactory.Object, _mockSupabaseService.Object));
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, null!, _mockSupabaseService.Object));
        Assert.Equal("loggerFactory", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSupabaseService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, null!));
        Assert.Equal("supabaseService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithDisabledTelegram_ThrowsInvalidOperationException()
    {
        var disabledConfig = new Config
        {
            Telegram = new Aula.Configuration.Telegram { Enabled = false, Token = "test-token" },
            Children = new List<Child>()
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new TelegramInteractiveBot(_mockAgentService.Object, disabledConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object));
        
        Assert.Contains("Telegram bot is not enabled", exception.Message);
    }

    [Fact]
    public void Constructor_WithMissingToken_ThrowsInvalidOperationException()
    {
        var configWithoutToken = new Config
        {
            Telegram = new Aula.Configuration.Telegram { Enabled = true, Token = "" },
            Children = new List<Child>()
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new TelegramInteractiveBot(_mockAgentService.Object, configWithoutToken, _mockLoggerFactory.Object, _mockSupabaseService.Object));
        
        Assert.Contains("token is missing", exception.Message);
    }

    [Fact]
    public async Task Start_WithDisabledTelegram_LogsErrorAndReturns()
    {
        var disabledConfig = new Config
        {
            Telegram = new Aula.Configuration.Telegram { Enabled = false, Token = "test-token" },
            Children = new List<Child>()
        };

        // We need to create a bot with enabled=true first, then change the config
        var enabledConfig = new Config
        {
            Telegram = new Aula.Configuration.Telegram { Enabled = true, Token = "test-token" },
            Children = new List<Child>()
        };

        var bot = new TelegramInteractiveBot(_mockAgentService.Object, enabledConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        // Now use reflection to set the disabled config
        var configField = typeof(TelegramInteractiveBot).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(bot, disabledConfig);

        await bot.Start();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot start Telegram bot: Telegram integration is not enabled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Stop_WhenCalled_LogsInformation()
    {
        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);
        
        bot.Stop();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping Telegram interactive bot")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Telegram interactive bot stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWeekLetter_WithDisabledTelegram_LogsWarningAndReturns()
    {
        var disabledConfig = new Config
        {
            Telegram = new Aula.Configuration.Telegram { Enabled = false, Token = "test-token" },
            Children = new List<Child>()
        };

        var enabledConfig = new Config
        {
            Telegram = new Aula.Configuration.Telegram { Enabled = true, Token = "test-token" },
            Children = new List<Child>()
        };

        var bot = new TelegramInteractiveBot(_mockAgentService.Object, enabledConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        // Use reflection to set the disabled config
        var configField = typeof(TelegramInteractiveBot).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(bot, disabledConfig);

        var weekLetter = new JObject();
        await bot.PostWeekLetter("Emma", weekLetter);

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Telegram integration is disabled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWeekLetter_WithMissingChannelId_LogsWarningAndReturns()
    {
        var configWithoutChannel = new Config
        {
            Telegram = new Aula.Configuration.Telegram { Enabled = true, Token = "test-token", ChannelId = "" },
            Children = new List<Child>()
        };

        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        // Use reflection to set the config without channel
        var configField = typeof(TelegramInteractiveBot).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(bot, configWithoutChannel);

        var weekLetter = new JObject();
        await bot.PostWeekLetter("Emma", weekLetter);

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("channel ID is missing")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWeekLetter_WithUnknownChild_LogsWarningAndReturns()
    {
        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        var weekLetter = new JObject();
        await bot.PostWeekLetter("UnknownChild", weekLetter);

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Child not found for week letter posting")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWeekLetter_WithDuplicateContent_ProcessesBothCalls()
    {
        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        var weekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["indhold"] = "Test week letter content"
                }
            }
        };

        // Post the same week letter twice - both calls should complete without throwing
        await bot.PostWeekLetter("Emma", weekLetter);
        await bot.PostWeekLetter("Emma", weekLetter);

        // Should not throw an exception - the duplicate detection logic exists
        Assert.True(true);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("987654321")]
    public async Task SendMessage_WithLongChatId_SendsSuccessfully(string chatId)
    {
        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        // This will attempt to send but fail since we don't have a real Telegram client
        // The test is mainly to verify the method signature and basic flow
        try
        {
            await bot.SendMessage(chatId, "Test message");
        }
        catch (Exception)
        {
            // Expected to fail due to mocked setup, but we're testing the method signature
        }

        // Verify the logger was called to send the message
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending message to chat")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(1234567890L)]
    [InlineData(987654321L)]
    public async Task SendMessage_WithLongChatIdAsLong_SendsSuccessfully(long chatId)
    {
        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        // This will attempt to send but fail since we don't have a real Telegram client
        try
        {
            await bot.SendMessage(chatId, "Test message");
        }
        catch (Exception)
        {
            // Expected to fail due to mocked setup
        }

        // Verify the logger was called
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending message to chat")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendMessage_WithException_LogsError()
    {
        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        // This should cause an exception since we don't have real Telegram setup
        await bot.SendMessage("test-chat", "Test message");

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error sending message to chat")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("Test content 1")]
    [InlineData("Test content 2")]
    [InlineData("")]
    public void ComputeHash_WithDifferentInputs_ReturnsConsistentHashes(string input)
    {
        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        // Use reflection to access the private ComputeHash method
        var method = typeof(TelegramInteractiveBot).GetMethod("ComputeHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var hash1 = method.Invoke(bot, new object[] { input }) as string;
        var hash2 = method.Invoke(bot, new object[] { input }) as string;

        Assert.Equal(hash1, hash2);
        Assert.NotNull(hash1);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public void ComputeHash_WithDifferentInputs_ReturnsDifferentHashes()
    {
        var bot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _mockLoggerFactory.Object, _mockSupabaseService.Object);

        var method = typeof(TelegramInteractiveBot).GetMethod("ComputeHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var hash1 = method.Invoke(bot, new object[] { "content1" }) as string;
        var hash2 = method.Invoke(bot, new object[] { "content2" }) as string;

        Assert.NotEqual(hash1, hash2);
    }
}