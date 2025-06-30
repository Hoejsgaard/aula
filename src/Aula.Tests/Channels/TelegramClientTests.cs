using Newtonsoft.Json.Linq;
using Aula.Channels;
using Aula.Configuration;
using ConfigTelegram = Aula.Configuration.Telegram;

namespace Aula.Tests.Channels;

public class TelegramClientTests
{
    [Fact]
    public void Constructor_WithConfig_EnabledTrue_CreatesInstance()
    {
        // Arrange
        var config = new Config
        {
            Telegram = new ConfigTelegram
            {
                Enabled = true,
                Token = "123456789:AABBCCDDEEFFGG"
            }
        };

        // Act & Assert
        var client = new TelegramClient(config);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithConfig_EnabledFalse_CreatesInstance()
    {
        // Arrange
        var config = new Config
        {
            Telegram = new ConfigTelegram
            {
                Enabled = false,
                Token = ""
            }
        };

        // Act & Assert
        var client = new TelegramClient(config);
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithToken_CreatesInstance()
    {
        // Arrange
        var token = "123456789:AABBCCDDEEFFGG";

        // Act & Assert
        var client = new TelegramClient(token);
        Assert.NotNull(client);
    }

    [Theory]
    [InlineData("123456789:AABBCCDDEEFFGG")]
    [InlineData("987654321:XXYYZZ")]
    [InlineData("")]
    public void Constructor_WithVariousTokens_DoesNotThrow(string token)
    {
        // Act & Assert
        var client = new TelegramClient(token);
        Assert.NotNull(client);
    }

    [Fact]
    public async Task SendMessageToChannel_WithDisabledTelegram_ReturnsFalse()
    {
        // Arrange
        var config = new Config
        {
            Telegram = new ConfigTelegram { Enabled = false }
        };
        var client = new TelegramClient(config);

        // Act
        var result = await client.SendMessageToChannel("@testchannel", "Test message");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task PostWeekLetter_WithDisabledTelegram_ReturnsFalse()
    {
        // Arrange
        var config = new Config
        {
            Telegram = new ConfigTelegram { Enabled = false }
        };
        var client = new TelegramClient(config);
        var weekLetter = CreateSampleWeekLetter();
        var child = new Child { FirstName = "Alice", LastName = "Johnson", Colour = "#FF0000" };

        // Act
        var result = await client.PostWeekLetter("@testchannel", weekLetter, child);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PostWeekLetter_WithValidInputs_DoesNotThrow()
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var weekLetter = CreateSampleWeekLetter();
        var child = new Child
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Colour = "#FF0000"
        };

        // Act & Assert - Should not throw when constructing message
        var task = client.PostWeekLetter("@testchannel", weekLetter, child);
        Assert.NotNull(task);
    }

    [Fact]
    public void PostWeekLetter_WithEmptyWeekLetter_HandlesGracefully()
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var emptyWeekLetter = new JObject();
        var child = new Child
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Colour = "#FF0000"
        };

        // Act & Assert
        var task = client.PostWeekLetter("@testchannel", emptyWeekLetter, child);
        Assert.NotNull(task);
    }

    [Fact]
    public void PostWeekLetter_WithMissingFields_HandlesGracefully()
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var incompleteWeekLetter = JObject.Parse(@"{
            ""ugebreve"": [{}]
        }");
        var child = new Child
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Colour = "#FF0000"
        };

        // Act & Assert
        var task = client.PostWeekLetter("@testchannel", incompleteWeekLetter, child);
        Assert.NotNull(task);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Alice")]
    [InlineData(TestChild1)]
    [InlineData("TestChild2")]
    public void PostWeekLetter_WithVariousChildNames_HandlesCorrectly(string firstName)
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var weekLetter = CreateSampleWeekLetter();
        var child = new Child
        {
            FirstName = firstName,
            LastName = "Johnson",
            Colour = "#FF0000"
        };

        // Act & Assert
        var task = client.PostWeekLetter("@testchannel", weekLetter, child);
        Assert.NotNull(task);
    }

    [Theory]
    [InlineData("@testchannel")]
    [InlineData("-1001234567890")]
    [InlineData("123456789")]
    [InlineData("")]
    public void SendMessageToChannel_WithVariousChannelIds_DoesNotThrow(string channelId)
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");

        // Act & Assert
        var task = client.SendMessageToChannel(channelId, "Test message");
        Assert.NotNull(task);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Simple message")]
    [InlineData("Message with <b>bold</b> text")]
    [InlineData("Message with <i>italic</i> text")]
    [InlineData("Message with special chars: @#$%")]
    public void SendMessageToChannel_WithVariousMessages_DoesNotThrow(string message)
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");

        // Act & Assert
        var task = client.SendMessageToChannel("@testchannel", message);
        Assert.NotNull(task);
    }

    private static JObject CreateSampleWeekLetter()
    {
        return JObject.Parse(@"{
            ""ugebreve"": [
                {
                    ""uge"": ""25"",
                    ""klasseNavn"": ""1.A"",
                    ""indhold"": ""<p>This is a test week letter with <strong>bold</strong> text and <em>italic</em> text.</p><div>A div with content</div><br/><p>Another paragraph.</p>""
                }
            ]
        }");
    }
}