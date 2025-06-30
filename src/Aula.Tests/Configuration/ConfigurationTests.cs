using Microsoft.Extensions.Configuration;
using System.IO;
using Aula.Configuration;
using ConfigSlack = Aula.Configuration.Slack;
using ConfigTelegram = Aula.Configuration.Telegram;

namespace Aula.Tests.Configuration;

public class ConfigurationTests
{
    private readonly IConfiguration _configuration;
    private readonly Config _config;

    public ConfigurationTests()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "test-appsettings.json");

        _configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        _config = new Config();
        _configuration.Bind(_config);
    }

    [Fact]
    public void Configuration_LoadsSuccessfully()
    {
        // Assert
        Assert.NotNull(_config);
        Assert.NotNull(_config.UniLogin);
        Assert.NotNull(_config.Slack);
        Assert.NotNull(_config.Telegram);
        Assert.NotNull(_config.OpenAi);
        Assert.NotNull(_config.Children);
        Assert.NotNull(_config.GoogleServiceAccount);
        Assert.NotNull(_config.Supabase);
    }

    [Fact]
    public void UniLogin_ConfigurationIsCorrect()
    {
        // Assert
        Assert.Equal("test-user", _config.UniLogin.Username);
        Assert.Equal("REDACTED_PASSWORD", _config.UniLogin.Password);
    }

    [Fact]
    public void Slack_ConfigurationIsCorrect()
    {
        // Assert
        Assert.Equal("https://hooks.slack.com/test", _config.Slack.WebhookUrl);
        Assert.Equal("xoxb-test-token", _config.Slack.ApiToken);
        Assert.True(_config.Slack.EnableInteractiveBot);
        Assert.Equal("C1234567890", _config.Slack.ChannelId);
        Assert.True(_config.Slack.PostWeekLettersOnStartup);
    }

    [Fact]
    public void Telegram_ConfigurationIsCorrect()
    {
        // Assert
        Assert.True(_config.Telegram.Enabled);
        Assert.Equal("TestBot", _config.Telegram.BotName);
        Assert.Equal("123456789:AABBCCDDEEFFGGHHTest", _config.Telegram.Token);
        Assert.Equal("@testchannel", _config.Telegram.ChannelId);
        Assert.False(_config.Telegram.PostWeekLettersOnStartup);
    }

    [Fact]
    public void OpenAi_ConfigurationIsCorrect()
    {
        // Assert
        Assert.Equal("sk-test-key", _config.OpenAi.ApiKey);
        Assert.Equal("gpt-4", _config.OpenAi.Model);
    }

    [Fact]
    public void Children_ConfigurationIsCorrect()
    {
        // Assert
        Assert.Equal(2, _config.Children.Count);

        var alice = _config.Children.First(c => c.FirstName == "Alice");
        Assert.Equal("Alice", alice.FirstName);
        Assert.Equal("Johnson", alice.LastName);
        Assert.Equal("blue", alice.Colour);
        Assert.Equal("alice@example.com", alice.GoogleCalendarId);

        var bob = _config.Children.First(c => c.FirstName == "Bob");
        Assert.Equal("Bob", bob.FirstName);
        Assert.Equal("Smith", bob.LastName);
        Assert.Equal("red", bob.Colour);
        Assert.Equal("bob@example.com", bob.GoogleCalendarId);
    }

    [Fact]
    public void GoogleServiceAccount_ConfigurationIsCorrect()
    {
        // Assert
        Assert.Equal("service_account", _config.GoogleServiceAccount.Type);
        Assert.Equal("test-project", _config.GoogleServiceAccount.ProjectId);
        Assert.Equal("test-key-id", _config.GoogleServiceAccount.PrivateKeyId);
        Assert.Contains("BEGIN PRIVATE KEY", _config.GoogleServiceAccount.PrivateKey);
        Assert.Equal("test@test-project.iam.gserviceaccount.com", _config.GoogleServiceAccount.ClientEmail);
        Assert.Equal("123456789", _config.GoogleServiceAccount.ClientId);
        Assert.Equal("https://accounts.google.com/o/oauth2/auth", _config.GoogleServiceAccount.AuthUri);
        Assert.Equal("https://oauth2.googleapis.com/token", _config.GoogleServiceAccount.TokenUri);
        Assert.Equal("googleapis.com", _config.GoogleServiceAccount.UniverseDomain);
    }

    [Fact]
    public void Supabase_ConfigurationIsCorrect()
    {
        // Assert
        Assert.Equal("https://test.supabase.co", _config.Supabase.Url);
        Assert.Equal("test-public-key", _config.Supabase.Key);
        Assert.Equal("test-service-role-key", _config.Supabase.ServiceRoleKey);
    }

    [Fact]
    public void Config_DefaultValues_AreApplied()
    {
        // Arrange
        var defaultConfig = new Config();

        // Assert - Check default values are as expected
        Assert.Empty(defaultConfig.UniLogin.Username);
        Assert.Empty(defaultConfig.UniLogin.Password);
        Assert.False(defaultConfig.Slack.EnableInteractiveBot);
        Assert.True(defaultConfig.Slack.PostWeekLettersOnStartup); // Default to true for backward compatibility
        Assert.False(defaultConfig.Telegram.Enabled);
        Assert.False(defaultConfig.Telegram.PostWeekLettersOnStartup); // Default to false
        Assert.Equal("gpt-4", defaultConfig.OpenAi.Model); // Default to GPT-4
        Assert.Empty(defaultConfig.Children);
    }

    [Fact]
    public void Config_WithEmptyConfiguration_UsesDefaults()
    {
        // Arrange
        var emptyConfiguration = new ConfigurationBuilder().Build();
        var emptyConfig = new Config();
        emptyConfiguration.Bind(emptyConfig);

        // Assert - Should use default values when configuration is empty
        Assert.Empty(emptyConfig.UniLogin.Username);
        Assert.Empty(emptyConfig.Slack.WebhookUrl);
        Assert.False(emptyConfig.Telegram.Enabled);
        Assert.Equal("gpt-4", emptyConfig.OpenAi.Model);
        Assert.Empty(emptyConfig.Children);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UniLogin_WithInvalidCredentials_HandlesGracefully(string? invalidValue)
    {
        // Arrange
        var config = new Config
        {
            UniLogin = new UniLogin
            {
                Username = invalidValue ?? string.Empty,
                Password = invalidValue ?? string.Empty
            }
        };

        // Assert - Should not throw and handle empty values gracefully
        Assert.NotNull(config.UniLogin);
        Assert.Equal(invalidValue ?? string.Empty, config.UniLogin.Username);
        Assert.Equal(invalidValue ?? string.Empty, config.UniLogin.Password);
    }

    [Theory]
    [InlineData("C1234567890", true)] // Valid Slack channel ID
    [InlineData("#general", true)] // Channel name
    [InlineData("@username", true)] // DM to user
    [InlineData("", false)] // Empty
    [InlineData("   ", false)] // Whitespace
    public void Slack_ChannelId_ValidationTests(string channelId, bool isValid)
    {
        // Arrange
        var config = new Config
        {
            Slack = new ConfigSlack
            {
                ChannelId = channelId,
                EnableInteractiveBot = true,
                ApiToken = "test-token"
            }
        };

        // Act & Assert
        if (isValid)
        {
            Assert.False(string.IsNullOrWhiteSpace(config.Slack.ChannelId));
        }
        else
        {
            Assert.True(string.IsNullOrWhiteSpace(config.Slack.ChannelId));
        }
    }

    [Theory]
    [InlineData("123456789:AABBCCDDEEFFGG", true)] // Valid format
    [InlineData("123456789:AABBCCDDEEFFGGHHTest", true)] // Test token format
    [InlineData("invalid-token", false)] // Invalid format
    [InlineData("", false)] // Empty
    [InlineData("123456789", false)] // Missing colon and secret
    public void Telegram_Token_ValidationTests(string token, bool isValidFormat)
    {
        // Arrange
        var config = new Config
        {
            Telegram = new ConfigTelegram
            {
                Token = token,
                Enabled = true
            }
        };

        // Act & Assert
        var hasColonAndMinLength = !string.IsNullOrEmpty(token) &&
                                   token.Contains(':') &&
                                   token.Length > 10;

        Assert.Equal(isValidFormat, hasColonAndMinLength);
    }

    [Fact]
    public void Children_WithMultipleChildren_MaintainsOrder()
    {
        // Assert - Children should maintain the order from configuration
        Assert.Equal("Alice", _config.Children[0].FirstName);
        Assert.Equal("Bob", _config.Children[1].FirstName);
    }

    [Fact]
    public void GoogleServiceAccount_RequiredFields_ArePresent()
    {
        // Assert - Essential fields for Google service account
        Assert.False(string.IsNullOrEmpty(_config.GoogleServiceAccount.ProjectId));
        Assert.False(string.IsNullOrEmpty(_config.GoogleServiceAccount.ClientEmail));
        Assert.False(string.IsNullOrEmpty(_config.GoogleServiceAccount.PrivateKey));
        Assert.Contains("@", _config.GoogleServiceAccount.ClientEmail); // Should be email format
    }

    [Fact]
    public void Supabase_RequiredFields_ArePresent()
    {
        // Assert - Essential fields for Supabase connection
        Assert.False(string.IsNullOrEmpty(_config.Supabase.Url));
        Assert.False(string.IsNullOrEmpty(_config.Supabase.ServiceRoleKey));
        Assert.StartsWith("https://", _config.Supabase.Url); // Should be HTTPS URL
    }

    [Fact]
    public void Config_IConfig_Interface_IsImplemented()
    {
        // Assert - Config should implement IConfig interface
        Assert.IsAssignableFrom<IConfig>(_config);

        // Verify all properties are accessible through interface
        IConfig iConfig = _config;
        Assert.NotNull(iConfig.UniLogin);
        Assert.NotNull(iConfig.Slack);
        Assert.NotNull(iConfig.Telegram);
        Assert.NotNull(iConfig.OpenAi);
        Assert.NotNull(iConfig.Children);
        Assert.NotNull(iConfig.GoogleServiceAccount);
        Assert.NotNull(iConfig.Supabase);
    }
}