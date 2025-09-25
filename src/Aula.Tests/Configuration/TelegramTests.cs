using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Configuration;

public class TelegramTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var telegram = new Aula.Configuration.Telegram();

        // Assert
        Assert.False(telegram.Enabled);
        Assert.NotNull(telegram.BotName);
        Assert.Equal(string.Empty, telegram.BotName);
        Assert.NotNull(telegram.Token);
        Assert.Equal(string.Empty, telegram.Token);
        Assert.NotNull(telegram.ChannelId);
        Assert.Equal(string.Empty, telegram.ChannelId);
        Assert.True(telegram.EnableInteractiveBot);
    }

    [Fact]
    public void Enabled_CanSetAndGetValue()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.Enabled = true;

        // Assert
        Assert.True(telegram.Enabled);
    }

    [Fact]
    public void BotName_CanSetAndGetValue()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();
        var testBotName = "AulaBot";

        // Act
        telegram.BotName = testBotName;

        // Assert
        Assert.Equal(testBotName, telegram.BotName);
    }

    [Fact]
    public void Token_CanSetAndGetValue()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();
        var testToken = "123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk";

        // Act
        telegram.Token = testToken;

        // Assert
        Assert.Equal(testToken, telegram.Token);
    }

    [Fact]
    public void ChannelId_CanSetAndGetValue()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();
        var testChannelId = "@family_channel";

        // Act
        telegram.ChannelId = testChannelId;

        // Assert
        Assert.Equal(testChannelId, telegram.ChannelId);
    }

    [Fact]
    public void EnableInteractiveBot_CanSetAndGetValue()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.EnableInteractiveBot = false;

        // Assert
        Assert.False(telegram.EnableInteractiveBot);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AulaBot")]
    [InlineData("FamilyAssistant")]
    [InlineData("SchoolBot")]
    [InlineData("bot_with_underscores")]
    [InlineData("BotWithNumbers123")]
    public void BotName_AcceptsVariousFormats(string botName)
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.BotName = botName;

        // Assert
        Assert.Equal(botName, telegram.BotName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk")]
    [InlineData("987654321:XYZabcdefghijklmnopqrstuvwxyzABCDEFGHIJK")]
    [InlineData("token_without_colon")]
    [InlineData("very-long-token-with-many-characters")]
    public void Token_AcceptsVariousFormats(string token)
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.Token = token;

        // Assert
        Assert.Equal(token, telegram.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@family_channel")]
    [InlineData("@school_updates")]
    [InlineData("-1001234567890")]
    [InlineData("-100987654321")]
    [InlineData("1234567890")]
    [InlineData("family_group")]
    public void ChannelId_AcceptsVariousFormats(string channelId)
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.ChannelId = channelId;

        // Assert
        Assert.Equal(channelId, telegram.ChannelId);
    }

    [Fact]
    public void AllProperties_CanBeSetSimultaneously()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();
        var enabled = true;
        var botName = "TestBot";
        var token = "123456789:TestToken";
        var channelId = "@test_channel";
        var enableInteractiveBot = false;

        // Act
        telegram.Enabled = enabled;
        telegram.BotName = botName;
        telegram.Token = token;
        telegram.ChannelId = channelId;
        telegram.EnableInteractiveBot = enableInteractiveBot;

        // Assert
        Assert.Equal(enabled, telegram.Enabled);
        Assert.Equal(botName, telegram.BotName);
        Assert.Equal(token, telegram.Token);
        Assert.Equal(channelId, telegram.ChannelId);
        Assert.Equal(enableInteractiveBot, telegram.EnableInteractiveBot);
    }

    [Fact]
    public void Telegram_ObjectInitializerSyntaxWorks()
    {
        // Arrange & Act
        var telegram = new Aula.Configuration.Telegram
        {
            Enabled = true,
            BotName = "InitBot",
            Token = "init:token",
            ChannelId = "@init_channel",
            EnableInteractiveBot = false
        };

        // Assert
        Assert.True(telegram.Enabled);
        Assert.Equal("InitBot", telegram.BotName);
        Assert.Equal("init:token", telegram.Token);
        Assert.Equal("@init_channel", telegram.ChannelId);
        Assert.False(telegram.EnableInteractiveBot);
    }

    [Fact]
    public void Telegram_HasCorrectNamespace()
    {
        // Arrange
        var type = typeof(Aula.Configuration.Telegram);

        // Act & Assert
        Assert.Equal("Aula.Configuration", type.Namespace);
    }

    [Fact]
    public void Telegram_IsPublicClass()
    {
        // Arrange
        var type = typeof(Aula.Configuration.Telegram);

        // Act & Assert
        Assert.True(type.IsPublic);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void Telegram_HasParameterlessConstructor()
    {
        // Arrange
        var type = typeof(Aula.Configuration.Telegram);

        // Act
        var constructor = type.GetConstructor(System.Type.EmptyTypes);

        // Assert
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void Telegram_PropertiesAreIndependent()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.Enabled = true;
        // Other properties should remain at their defaults

        // Assert
        Assert.True(telegram.Enabled);
        Assert.Equal(string.Empty, telegram.BotName); // Should remain default
        Assert.Equal(string.Empty, telegram.Token); // Should remain default
        Assert.Equal(string.Empty, telegram.ChannelId); // Should remain default
        Assert.True(telegram.EnableInteractiveBot); // Should remain default
    }

    [Fact]
    public void BotName_CanBeSetToNull()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.BotName = null!;

        // Assert
        Assert.Null(telegram.BotName);
    }

    [Fact]
    public void Token_CanBeSetToNull()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.Token = null!;

        // Assert
        Assert.Null(telegram.Token);
    }

    [Fact]
    public void ChannelId_CanBeSetToNull()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.ChannelId = null!;

        // Assert
        Assert.Null(telegram.ChannelId);
    }

    [Fact]
    public void Telegram_SupportsCommonTelegramTokenFormats()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act & Assert - Standard bot token format
        telegram.Token = "123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk";
        Assert.Equal("123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk", telegram.Token);
        Assert.Contains(":", telegram.Token);

        // Act & Assert - Different bot token format
        telegram.Token = "987654321:XYZabcdefghijklmnopqrstuvwxyzABCDEFGHIJK";
        Assert.Equal("987654321:XYZabcdefghijklmnopqrstuvwxyzABCDEFGHIJK", telegram.Token);
        Assert.Contains(":", telegram.Token);
    }

    [Fact]
    public void Telegram_SupportsCommonChannelIdFormats()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act & Assert - Channel username format
        telegram.ChannelId = "@family_channel";
        Assert.Equal("@family_channel", telegram.ChannelId);
        Assert.StartsWith("@", telegram.ChannelId);

        // Act & Assert - Supergroup chat ID format
        telegram.ChannelId = "-1001234567890";
        Assert.Equal("-1001234567890", telegram.ChannelId);
        Assert.StartsWith("-100", telegram.ChannelId);

        // Act & Assert - Regular chat ID format
        telegram.ChannelId = "1234567890";
        Assert.Equal("1234567890", telegram.ChannelId);
    }

    [Fact]
    public void Telegram_BooleanFlagsWorkIndependently()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act
        telegram.Enabled = true;
        telegram.EnableInteractiveBot = false;

        // Assert
        Assert.True(telegram.Enabled);
        Assert.False(telegram.EnableInteractiveBot);
    }

    [Fact]
    public void Telegram_CanToggleBooleanProperties()
    {
        // Arrange
        var telegram = new Aula.Configuration.Telegram();

        // Act & Assert - Toggle Enabled
        Assert.False(telegram.Enabled);
        telegram.Enabled = true;
        Assert.True(telegram.Enabled);
        telegram.Enabled = false;
        Assert.False(telegram.Enabled);

        // Act & Assert - Toggle EnableInteractiveBot
        Assert.True(telegram.EnableInteractiveBot);
        telegram.EnableInteractiveBot = false;
        Assert.False(telegram.EnableInteractiveBot);
        telegram.EnableInteractiveBot = true;
        Assert.True(telegram.EnableInteractiveBot);
    }
}