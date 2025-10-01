using Microsoft.Extensions.Logging;
using Moq;
using Aula.Bots;
using Aula.Configuration;
using Aula.Services;
using Aula.Integration;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests.Bots;

// Test implementation of abstract BotBase
public class TestBot : BotBase
{
    public bool ValidateConfigurationCalled { get; private set; }
    public bool InitializePlatformCalled { get; private set; }
    public bool SendWelcomeMessageCalled { get; private set; }
    public bool StartMessageProcessingCalled { get; private set; }
    public bool StopMessageProcessingCalled { get; private set; }
    public string? LastWeekLetterChildName { get; set; }
    public string? LastWeekLetterContent { get; set; }

    public TestBot(IAgentService agentService, Config config, ILogger logger)
        : base(agentService, config, logger)
    {
    }

    protected override string GetPlatformType() => "Test";

    protected override Task ValidateConfiguration()
    {
        ValidateConfigurationCalled = true;
        return Task.CompletedTask;
    }

    protected override Task InitializePlatform()
    {
        InitializePlatformCalled = true;
        return Task.CompletedTask;
    }

    protected override Task SendWelcomeMessage()
    {
        SendWelcomeMessageCalled = true;
        return Task.CompletedTask;
    }

    protected override Task StartMessageProcessing()
    {
        StartMessageProcessingCalled = true;
        return Task.CompletedTask;
    }

    protected override void StopMessageProcessing()
    {
        StopMessageProcessingCalled = true;
    }

    protected override Task SendWeekLetterMessage(string childName, string weekLetter)
    {
        LastWeekLetterChildName = childName;
        LastWeekLetterContent = weekLetter;
        return Task.CompletedTask;
    }
}

// Test implementation that throws during validation
public class ThrowingTestBot : TestBot
{
    public ThrowingTestBot(IAgentService agentService, Config config, ILogger logger)
        : base(agentService, config, logger)
    {
    }

    protected override Task ValidateConfiguration()
    {
        throw new Exception("Validation failed");
    }
}

public class BotBaseTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly Config _testConfig;

    public BotBaseTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<TestBot>();

        _testConfig = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child>
                {
                    new Child { FirstName = "Emma", LastName = "Doe" },
                    new Child { FirstName = "Oliver", LastName = "Doe" }
                }
            }
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        Assert.NotNull(bot);
    }

    [Fact]
    public void Constructor_WithNullAgentService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestBot(null!, _testConfig, _logger));
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestBot(_mockAgentService.Object, null!, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestBot(_mockAgentService.Object, _testConfig, null!));
    }

    [Fact]
    public void Constructor_WithDuplicateChildNames_ThrowsInvalidOperationException()
    {
        var configWithDuplicates = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child>
                {
                    new Child { FirstName = "Emma", LastName = "Doe" },
                    new Child { FirstName = "Emma", LastName = "Smith" } // Duplicate first name
				}
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new TestBot(_mockAgentService.Object, configWithDuplicates, _logger));

        Assert.Contains("Duplicate child first name: 'Emma'", ex.Message);
    }

    [Fact]
    public void Constructor_WithCaseInsensitiveDuplicateChildNames_ThrowsInvalidOperationException()
    {
        var configWithDuplicates = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child>
                {
                    new Child { FirstName = "Emma", LastName = "Doe" },
                    new Child { FirstName = "EMMA", LastName = "Smith" } // Case-insensitive duplicate
				}
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new TestBot(_mockAgentService.Object, configWithDuplicates, _logger));

        Assert.Contains("Duplicate child first name: 'EMMA'", ex.Message);
    }

    [Fact]
    public async Task Start_CallsAllInitializationMethods()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        await bot.Start();

        Assert.True(bot.ValidateConfigurationCalled);
        Assert.True(bot.InitializePlatformCalled);
        Assert.True(bot.SendWelcomeMessageCalled);
        Assert.True(bot.StartMessageProcessingCalled);
    }

    [Fact]
    public async Task Start_WhenValidateConfigurationThrows_RethrowsException()
    {
        // Create a test bot that throws during validation
        var throwingBot = new ThrowingTestBot(_mockAgentService.Object, _testConfig, _logger);

        await Assert.ThrowsAsync<Exception>(() => throwingBot.Start());
    }

    [Fact]
    public void Stop_CallsStopMessageProcessing()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        bot.Stop();

        Assert.True(bot.StopMessageProcessingCalled);
    }

    [Fact]
    public async Task PostWeekLetter_WithValidContent_SendsMessage()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        await bot.PostWeekLetter("Emma", "Week letter content");

        Assert.Equal("Emma", bot.LastWeekLetterChildName);
        Assert.Equal("Week letter content", bot.LastWeekLetterContent);
    }

    [Fact]
    public async Task PostWeekLetter_WithEmptyContent_DoesNotSendMessage()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        await bot.PostWeekLetter("Emma", "");

        Assert.Null(bot.LastWeekLetterChildName);
        Assert.Null(bot.LastWeekLetterContent);
    }

    [Fact]
    public async Task PostWeekLetter_WithNullContent_DoesNotSendMessage()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        await bot.PostWeekLetter("Emma", null!);

        Assert.Null(bot.LastWeekLetterChildName);
        Assert.Null(bot.LastWeekLetterContent);
    }

    [Fact]
    public async Task PostWeekLetter_WithDuplicateContent_SkipsSecondPost()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);
        const string content = "Same week letter content";

        // Post first time
        await bot.PostWeekLetter("Emma", content);
        Assert.Equal("Emma", bot.LastWeekLetterChildName);
        Assert.Equal(content, bot.LastWeekLetterContent);

        // Reset tracking
        bot.LastWeekLetterChildName = null;
        bot.LastWeekLetterContent = null;

        // Post same content again
        await bot.PostWeekLetter("Oliver", content);

        // Should not have sent the message again
        Assert.Null(bot.LastWeekLetterChildName);
        Assert.Null(bot.LastWeekLetterContent);
    }

    [Fact]
    public async Task PostWeekLetter_WithDifferentContent_SendsBothMessages()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        // Post first message
        await bot.PostWeekLetter("Emma", "First week letter");
        Assert.Equal("Emma", bot.LastWeekLetterChildName);
        Assert.Equal("First week letter", bot.LastWeekLetterContent);

        // Post different content
        await bot.PostWeekLetter("Oliver", "Different week letter");
        Assert.Equal("Oliver", bot.LastWeekLetterChildName);
        Assert.Equal("Different week letter", bot.LastWeekLetterContent);
    }

    [Fact]
    public void BuildWelcomeMessage_WithSingleChild_ContainsChildName()
    {
        var singleChildConfig = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child> { new Child { FirstName = "Emma", LastName = "Doe" } }
            }
        };

        var bot = new TestBot(_mockAgentService.Object, singleChildConfig, _logger);

        // Use reflection to call protected method
        var method = typeof(BotBase).GetMethod("BuildWelcomeMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (string)method!.Invoke(bot, null)!;

        Assert.Contains("Emma", result);
        Assert.Contains("Jeg er online", result);
        Assert.Contains("Uge", result);
    }

    [Fact]
    public void BuildWelcomeMessage_WithMultipleChildren_ContainsAllNames()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        // Use reflection to call protected method
        var method = typeof(BotBase).GetMethod("BuildWelcomeMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (string)method!.Invoke(bot, null)!;

        Assert.Contains("Emma", result);
        Assert.Contains("Oliver", result);
        Assert.Contains("og", result); // Danish "and"
    }

    [Fact]
    public void BuildWelcomeMessage_WithChildWithMultipleFirstNames_UsesFirstPart()
    {
        var configWithComplexName = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child> { new Child { FirstName = "Emma Louise", LastName = "Doe" } }
            }
        };

        var bot = new TestBot(_mockAgentService.Object, configWithComplexName, _logger);

        // Use reflection to call protected method
        var method = typeof(BotBase).GetMethod("BuildWelcomeMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (string)method!.Invoke(bot, null)!;

        Assert.Contains("Emma", result);
        Assert.DoesNotContain("Louise", result);
    }

    [Fact]
    public void ComputeWeekLetterHash_WithSameContent_ReturnsSameHash()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        // Use reflection to call protected method
        var method = typeof(BotBase).GetMethod("ComputeWeekLetterHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var hash1 = (string)method!.Invoke(bot, new object[] { "Same content" })!;
        var hash2 = (string)method!.Invoke(bot, new object[] { "Same content" })!;

        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public void ComputeWeekLetterHash_WithDifferentContent_ReturnsDifferentHash()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        // Use reflection to call protected method
        var method = typeof(BotBase).GetMethod("ComputeWeekLetterHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var hash1 = (string)method!.Invoke(bot, new object[] { "Content 1" })!;
        var hash2 = (string)method!.Invoke(bot, new object[] { "Content 2" })!;

        Assert.NotEqual(hash1, hash2);
        Assert.NotEmpty(hash1);
        Assert.NotEmpty(hash2);
    }

    [Fact]
    public void ComputeWeekLetterHash_ReturnsHexString()
    {
        var bot = new TestBot(_mockAgentService.Object, _testConfig, _logger);

        // Use reflection to call protected method
        var method = typeof(BotBase).GetMethod("ComputeWeekLetterHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var hash = (string)method!.Invoke(bot, new object[] { "Test content" })!;

        // SHA256 hash should be 64 hex characters
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9A-F]+$", hash); // Only hex characters
    }
}
