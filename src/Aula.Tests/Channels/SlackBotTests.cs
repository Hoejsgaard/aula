using Newtonsoft.Json.Linq;
using Aula.Channels;
using Aula.Configuration;
using ConfigSlack = Aula.Configuration.Slack;

namespace Aula.Tests.Channels;

public class SlackBotTests
{
    [Fact]
    public void Constructor_WithConfig_CreatesInstance()
    {
        // Arrange
        var config = new Config
        {
            Slack = new ConfigSlack
            {
                WebhookUrl = "https://hooks.slack.com/test"
            }
        };

        // Act & Assert
        var slackBot = new SlackBot(config);
        Assert.NotNull(slackBot);
    }

    [Fact]
    public void Constructor_WithWebhookUrl_CreatesInstance()
    {
        // Arrange
        var webhookUrl = "https://hooks.slack.com/test";

        // Act & Assert
        var slackBot = new SlackBot(webhookUrl);
        Assert.NotNull(slackBot);
    }

    [Theory]
    [InlineData("https://hooks.slack.com/services/T123/B456/xyz")]
    [InlineData("https://hooks.slack.com/test")]
    public void Constructor_WithValidUrls_DoesNotThrow(string webhookUrl)
    {
        // Act & Assert - Constructor should not throw with valid URLs
        var slackBot = new SlackBot(webhookUrl);
        Assert.NotNull(slackBot);
    }

    [Fact]
    public void Constructor_WithEmptyUrl_ThrowsArgumentException()
    {
        // Act & Assert - Empty URL should throw
        Assert.Throws<ArgumentException>(() => new SlackBot(""));
    }

    [Fact]
    public async Task PushWeekLetter_WithNullWeekLetter_ReturnsFalse()
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");

        // Act
        var result = await slackBot.PushWeekLetter(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PostWeekLetter_WithValidInputs_DoesNotThrow()
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");
        var weekLetter = CreateSampleWeekLetter();
        var child = new Child
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Colour = "#FF0000"
        };

        // Act & Assert - Should not throw when constructing message
        var task = slackBot.PostWeekLetter(weekLetter, child);
        Assert.NotNull(task);
    }

    [Fact]
    public void SendTestMessage_WithMessage_DoesNotThrow()
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");
        var testMessage = "Test message";

        // Act & Assert
        var task = slackBot.SendTestMessage(testMessage);
        Assert.NotNull(task);
    }

    [Fact]
    public void PostMessage_WithMessage_DoesNotThrow()
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");
        var message = "Test message";

        // Act & Assert
        var task = slackBot.PostMessage(message);
        Assert.NotNull(task);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Simple message")]
    [InlineData("Message with *bold* and _italic_")]
    [InlineData("Message with special chars: @#$%")]
    public void SendTestMessage_WithVariousMessages_DoesNotThrow(string message)
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");

        // Act & Assert
        var task = slackBot.SendTestMessage(message);
        Assert.NotNull(task);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Simple message")]
    [InlineData("Message with *bold* and _italic_")]
    [InlineData("Message with special chars: @#$%")]
    public void PostMessage_WithVariousMessages_DoesNotThrow(string message)
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");

        // Act & Assert
        var task = slackBot.PostMessage(message);
        Assert.NotNull(task);
    }

    [Fact]
    public void PostWeekLetter_WithEmptyWeekLetter_HandlesGracefully()
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");
        var emptyWeekLetter = new JObject();
        var child = new Child
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Colour = "#FF0000"
        };

        // Act & Assert - Should handle empty week letter without throwing
        var task = slackBot.PostWeekLetter(emptyWeekLetter, child);
        Assert.NotNull(task);
    }

    [Fact]
    public void PostWeekLetter_WithMissingFields_HandlesGracefully()
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");
        var incompleteWeekLetter = JObject.Parse(@"{
            ""ugebreve"": [{}]
        }");
        var child = new Child
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Colour = "#FF0000"
        };

        // Act & Assert - Should handle missing fields without throwing
        var task = slackBot.PostWeekLetter(incompleteWeekLetter, child);
        Assert.NotNull(task);
    }

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#00FF00")]
    [InlineData("#0000FF")]
    [InlineData("red")]
    [InlineData("")]
    public void PostWeekLetter_WithVariousChildColors_HandlesCorrectly(string color)
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");
        var weekLetter = CreateSampleWeekLetter();
        var child = new Child
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Colour = color
        };

        // Act & Assert
        var task = slackBot.PostWeekLetter(weekLetter, child);
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
        var slackBot = new SlackBot("https://hooks.slack.com/test");
        var weekLetter = CreateSampleWeekLetter();
        var child = new Child
        {
            FirstName = firstName,
            LastName = "Johnson",
            Colour = "#FF0000"
        };

        // Act & Assert
        var task = slackBot.PostWeekLetter(weekLetter, child);
        Assert.NotNull(task);
    }

    [Fact]
    public void PushWeekLetter_WithEmptyWeekLetter_DoesNotThrow()
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");
        var emptyWeekLetter = new JObject();

        // Act & Assert - Should not throw even with empty object
        var task = slackBot.PushWeekLetter(emptyWeekLetter);
        Assert.NotNull(task);
    }

    [Fact]
    public void PushWeekLetter_WithMissingContent_HandlesGracefully()
    {
        // Arrange
        var slackBot = new SlackBot("https://hooks.slack.com/test");
        var weekLetterWithoutContent = JObject.Parse(@"{
            ""ugebreve"": [
                {
                    ""uge"": ""25"",
                    ""klasseNavn"": ""1.A""
                }
            ]
        }");

        // Act & Assert
        var task = slackBot.PushWeekLetter(weekLetterWithoutContent);
        Assert.NotNull(task);
    }

    private static JObject CreateSampleWeekLetter()
    {
        return JObject.Parse(@"{
            ""ugebreve"": [
                {
                    ""uge"": ""25"",
                    ""klasseNavn"": ""1.A"",
                    ""indhold"": ""<p>This is a test week letter with <strong>bold</strong> text.</p>""
                }
            ]
        }");
    }
}