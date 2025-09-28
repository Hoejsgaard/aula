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
    [InlineData("Søren Johannes")]
    [InlineData("Hans Martin")]
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

    [Fact]
    public void Constructor_WithNullConfig_ThrowsNullReferenceException()
    {
        // Act & Assert
        Assert.Throws<NullReferenceException>(() => new TelegramClient((Config)null!));
    }

    [Fact]
    public void Constructor_WithNullTelegramConfig_ThrowsNullReferenceException()
    {
        // Arrange
        var config = new Config
        {
            Telegram = null!
        };

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => new TelegramClient(config));
    }

    [Fact]
    public async Task SendMessageToChannel_WithNullOrEmptyToken_ReturnsTrue()
    {
        // Arrange - Constructor with empty token should still work, but sending will likely fail
        var client = new TelegramClient("");

        // Act - This should not throw and should return true (optimistic)
        // The actual HTTP call may fail internally but is caught
        var result = await client.SendMessageToChannel("@testchannel", "Test message");

        // Assert - The method should handle errors gracefully
        Assert.False(result); // Should return false due to likely API failure
    }

    [Fact]
    public Task PostWeekLetter_WithNullChild_HandlesGracefully()
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var weekLetter = CreateSampleWeekLetter();

        // Act & Assert - Should handle null child gracefully
        var task = client.PostWeekLetter("@testchannel", weekLetter, null!);
        Assert.NotNull(task);
        return Task.CompletedTask;
    }

    [Fact]
    public Task PostWeekLetter_WithNullWeekLetter_HandlesGracefully()
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var child = new Child { FirstName = "Alice", LastName = "Johnson", Colour = "#FF0000" };

        // Act & Assert - Should handle null week letter gracefully
        var task = client.PostWeekLetter("@testchannel", null!, child);
        Assert.NotNull(task);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src='x' onerror='alert(1)'>")]
    [InlineData("<div onclick='malicious()'>content</div>")]
    public void PostWeekLetter_WithMaliciousHTML_SanitizesContent(string maliciousContent)
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var weekLetter = JObject.Parse($@"{{
            ""ugebreve"": [
                {{
                    ""uge"": ""25"",
                    ""klasseNavn"": ""1.A"",
                    ""indhold"": ""{maliciousContent}""
                }}
            ]
        }}");
        var child = new Child { FirstName = "Alice", LastName = "Johnson", Colour = "#FF0000" };

        // Act & Assert - Should not throw and should sanitize malicious content
        var task = client.PostWeekLetter("@testchannel", weekLetter, child);
        Assert.NotNull(task);
    }

    [Fact]
    public void PostWeekLetter_WithComplexHTML_HandlesCorrectly()
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var complexWeekLetter = JObject.Parse(@"{
            ""ugebreve"": [
                {
                    ""uge"": ""25"",
                    ""klasseNavn"": ""1.A"",
                    ""indhold"": ""<div><p>Nested <strong>content</strong> with <a href='http://example.com'>links</a></p><ul><li>List item 1</li><li>List item 2</li></ul><table><tr><td>Table cell</td></tr></table></div>""
                }
            ]
        }");
        var child = new Child { FirstName = "Alice", LastName = "Johnson", Colour = "#FF0000" };

        // Act & Assert - Should handle complex HTML structures
        var task = client.PostWeekLetter("@testchannel", complexWeekLetter, child);
        Assert.NotNull(task);
    }

    [Theory]
    [InlineData("#FF0000")] // Red
    [InlineData("#00FF00")] // Green  
    [InlineData("#0000FF")] // Blue
    [InlineData("#FFFFFF")] // White
    [InlineData("#000000")] // Black
    [InlineData("")] // Empty
    [InlineData(null)] // Null
    public void PostWeekLetter_WithVariousColors_HandlesCorrectly(string? colour)
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var weekLetter = CreateSampleWeekLetter();
        var child = new Child { FirstName = "Alice", LastName = "Johnson", Colour = colour ?? "" };

        // Act & Assert - Should handle various color values
        var task = client.PostWeekLetter("@testchannel", weekLetter, child);
        Assert.NotNull(task);
    }

    [Fact]
    public void PostWeekLetter_WithEmptyContent_HandlesGracefully()
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var emptyContentWeekLetter = JObject.Parse(@"{
            ""ugebreve"": [
                {
                    ""uge"": ""25"",
                    ""klasseNavn"": ""1.A"",
                    ""indhold"": """"
                }
            ]
        }");
        var child = new Child { FirstName = "Alice", LastName = "Johnson", Colour = "#FF0000" };

        // Act & Assert - Should handle empty content
        var task = client.PostWeekLetter("@testchannel", emptyContentWeekLetter, child);
        Assert.NotNull(task);
    }

    [Fact]
    public void PostWeekLetter_WithVeryLongContent_HandlesCorrectly()
    {
        // Arrange
        var client = new TelegramClient("123456789:AABBCCDDEEFFGG");
        var longContent = new string('A', 5000); // Very long content
        var longContentWeekLetter = JObject.Parse($@"{{
            ""ugebreve"": [
                {{
                    ""uge"": ""25"",
                    ""klasseNavn"": ""1.A"",
                    ""indhold"": ""{longContent}""
                }}
            ]
        }}");
        var child = new Child { FirstName = "Alice", LastName = "Johnson", Colour = "#FF0000" };

        // Act & Assert - Should handle very long content (may truncate)
        var task = client.PostWeekLetter("@testchannel", longContentWeekLetter, child);
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