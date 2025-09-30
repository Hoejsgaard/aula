using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Configuration;

public class SlackTests
{
	[Fact]
	public void Constructor_InitializesDefaultValues()
	{
		// Act
		var slack = new Aula.Configuration.Slack();

		// Assert
		Assert.NotNull(slack.WebhookUrl);
		Assert.Equal(string.Empty, slack.WebhookUrl);
		Assert.NotNull(slack.ApiToken);
		Assert.Equal(string.Empty, slack.ApiToken);
		Assert.False(slack.EnableInteractiveBot);
		Assert.NotNull(slack.ChannelId);
		Assert.Equal(string.Empty, slack.ChannelId);
		Assert.True(slack.Enabled);
		Assert.Equal("https://slack.com/api", slack.ApiBaseUrl);
	}

	[Fact]
	public void WebhookUrl_CanSetAndGetValue()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();
		var testWebhookUrl = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX";

		// Act
		slack.WebhookUrl = testWebhookUrl;

		// Assert
		Assert.Equal(testWebhookUrl, slack.WebhookUrl);
	}

	[Fact]
	public void ApiToken_CanSetAndGetValue()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();
		var testApiToken = "xoxb-fake-token-000000000000-000000000000-abcdefghijklm";

		// Act
		slack.ApiToken = testApiToken;

		// Assert
		Assert.Equal(testApiToken, slack.ApiToken);
	}

	[Fact]
	public void EnableInteractiveBot_CanSetAndGetValue()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.EnableInteractiveBot = true;

		// Assert
		Assert.True(slack.EnableInteractiveBot);
	}

	[Fact]
	public void ChannelId_CanSetAndGetValue()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();
		var testChannelId = "#family";

		// Act
		slack.ChannelId = testChannelId;

		// Assert
		Assert.Equal(testChannelId, slack.ChannelId);
	}

	[Fact]
	public void Enabled_CanSetAndGetValue()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.Enabled = false;

		// Assert
		Assert.False(slack.Enabled);
	}

	[Fact]
	public void ApiBaseUrl_CanSetAndGetValue()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();
		var testApiBaseUrl = "https://custom.slack.com/api";

		// Act
		slack.ApiBaseUrl = testApiBaseUrl;

		// Assert
		Assert.Equal(testApiBaseUrl, slack.ApiBaseUrl);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX")]
	[InlineData("https://hooks.slack.com/services/T12345678/B12345678/abcdefghijklmnopqrstuvwx")]
	[InlineData("https://custom-webhook-url.com/slack")]
	public void WebhookUrl_AcceptsVariousFormats(string webhookUrl)
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.WebhookUrl = webhookUrl;

		// Assert
		Assert.Equal(webhookUrl, slack.WebhookUrl);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("xoxb-fake-token-000000000000-000000000000-abcdefghijklm")]
	[InlineData("xoxp-fake-token-000000000000-000000000000-abcdefghijklm")]
	[InlineData("xoxs-fake-token-000000000000-000000000000-abcdefghijklm")]
	[InlineData("legacy-token-format")]
	public void ApiToken_AcceptsVariousFormats(string apiToken)
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.ApiToken = apiToken;

		// Assert
		Assert.Equal(apiToken, slack.ApiToken);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("#family")]
	[InlineData("#general")]
	[InlineData("C1234567890")]
	[InlineData("D1234567890")]
	[InlineData("@username")]
	[InlineData("channel-name")]
	public void ChannelId_AcceptsVariousFormats(string channelId)
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.ChannelId = channelId;

		// Assert
		Assert.Equal(channelId, slack.ChannelId);
	}

	[Theory]
	[InlineData("https://slack.com/api")]
	[InlineData("https://custom.slack.com/api")]
	[InlineData("https://enterprise.slack.com/api")]
	[InlineData("https://api.slack.com")]
	public void ApiBaseUrl_AcceptsVariousUrls(string apiBaseUrl)
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.ApiBaseUrl = apiBaseUrl;

		// Assert
		Assert.Equal(apiBaseUrl, slack.ApiBaseUrl);
	}

	[Fact]
	public void AllProperties_CanBeSetSimultaneously()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();
		var webhookUrl = "https://hooks.slack.com/test";
		var apiToken = "xoxb-test-token";
		var enableInteractiveBot = true;
		var channelId = "#test";
		var enabled = false;
		var apiBaseUrl = "https://test.slack.com/api";

		// Act
		slack.WebhookUrl = webhookUrl;
		slack.ApiToken = apiToken;
		slack.EnableInteractiveBot = enableInteractiveBot;
		slack.ChannelId = channelId;
		slack.Enabled = enabled;
		slack.ApiBaseUrl = apiBaseUrl;

		// Assert
		Assert.Equal(webhookUrl, slack.WebhookUrl);
		Assert.Equal(apiToken, slack.ApiToken);
		Assert.Equal(enableInteractiveBot, slack.EnableInteractiveBot);
		Assert.Equal(channelId, slack.ChannelId);
		Assert.Equal(enabled, slack.Enabled);
		Assert.Equal(apiBaseUrl, slack.ApiBaseUrl);
	}

	[Fact]
	public void Slack_ObjectInitializerSyntaxWorks()
	{
		// Arrange & Act
		var slack = new Aula.Configuration.Slack
		{
			WebhookUrl = "https://hooks.slack.com/init",
			ApiToken = "xoxb-init-token",
			EnableInteractiveBot = true,
			ChannelId = "#init",
			Enabled = false,
			ApiBaseUrl = "https://init.slack.com/api"
		};

		// Assert
		Assert.Equal("https://hooks.slack.com/init", slack.WebhookUrl);
		Assert.Equal("xoxb-init-token", slack.ApiToken);
		Assert.True(slack.EnableInteractiveBot);
		Assert.Equal("#init", slack.ChannelId);
		Assert.False(slack.Enabled);
		Assert.Equal("https://init.slack.com/api", slack.ApiBaseUrl);
	}

	[Fact]
	public void Slack_HasCorrectNamespace()
	{
		// Arrange
		var type = typeof(Aula.Configuration.Slack);

		// Act & Assert
		Assert.Equal("Aula.Configuration", type.Namespace);
	}

	[Fact]
	public void Slack_IsPublicClass()
	{
		// Arrange
		var type = typeof(Aula.Configuration.Slack);

		// Act & Assert
		Assert.True(type.IsPublic);
		Assert.False(type.IsAbstract);
		Assert.False(type.IsSealed);
	}

	[Fact]
	public void Slack_HasParameterlessConstructor()
	{
		// Arrange
		var type = typeof(Aula.Configuration.Slack);

		// Act
		var constructor = type.GetConstructor(System.Type.EmptyTypes);

		// Assert
		Assert.NotNull(constructor);
		Assert.True(constructor.IsPublic);
	}

	[Fact]
	public void Slack_PropertiesAreIndependent()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.WebhookUrl = "changed-webhook";
		// Other properties should remain at their defaults

		// Assert
		Assert.Equal("changed-webhook", slack.WebhookUrl);
		Assert.Equal(string.Empty, slack.ApiToken); // Should remain default
		Assert.False(slack.EnableInteractiveBot); // Should remain default
		Assert.Equal(string.Empty, slack.ChannelId); // Should remain default
		Assert.True(slack.Enabled); // Should remain default
		Assert.Equal("https://slack.com/api", slack.ApiBaseUrl); // Should remain default
	}

	[Fact]
	public void WebhookUrl_CanBeSetToNull()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.WebhookUrl = null!;

		// Assert
		Assert.Null(slack.WebhookUrl);
	}

	[Fact]
	public void ApiToken_CanBeSetToNull()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.ApiToken = null!;

		// Assert
		Assert.Null(slack.ApiToken);
	}

	[Fact]
	public void ChannelId_CanBeSetToNull()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.ChannelId = null!;

		// Assert
		Assert.Null(slack.ChannelId);
	}

	[Fact]
	public void ApiBaseUrl_CanBeSetToNull()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.ApiBaseUrl = null!;

		// Assert
		Assert.Null(slack.ApiBaseUrl);
	}

	[Fact]
	public void Slack_SupportsCommonWebhookUrlFormats()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act & Assert - Standard webhook URL format
		slack.WebhookUrl = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX";
		Assert.Equal("https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX", slack.WebhookUrl);
		Assert.StartsWith("https://hooks.slack.com/services/", slack.WebhookUrl);

		// Act & Assert - Different webhook URL format
		slack.WebhookUrl = "https://hooks.slack.com/services/T12345678/B12345678/abcdefghijklmnopqrstuvwx";
		Assert.Equal("https://hooks.slack.com/services/T12345678/B12345678/abcdefghijklmnopqrstuvwx", slack.WebhookUrl);
		Assert.StartsWith("https://hooks.slack.com/services/", slack.WebhookUrl);
	}

	[Fact]
	public void Slack_SupportsCommonTokenFormats()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act & Assert - Bot token format
		slack.ApiToken = "xoxb-fake-token-000000000000-000000000000-abcdefghijklm";
		Assert.Equal("xoxb-fake-token-000000000000-000000000000-abcdefghijklm", slack.ApiToken);
		Assert.StartsWith("xoxb-", slack.ApiToken);

		// Act & Assert - User token format
		slack.ApiToken = "xoxp-fake-token-000000000000-000000000000-abcdefghijklm";
		Assert.Equal("xoxp-fake-token-000000000000-000000000000-abcdefghijklm", slack.ApiToken);
		Assert.StartsWith("xoxp-", slack.ApiToken);

		// Act & Assert - Socket mode token format
		slack.ApiToken = "xoxs-fake-token-000000000000-000000000000-abcdefghijklm";
		Assert.Equal("xoxs-fake-token-000000000000-000000000000-abcdefghijklm", slack.ApiToken);
		Assert.StartsWith("xoxs-", slack.ApiToken);
	}

	[Fact]
	public void Slack_SupportsCommonChannelIdFormats()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act & Assert - Channel name format
		slack.ChannelId = "#family";
		Assert.Equal("#family", slack.ChannelId);
		Assert.StartsWith("#", slack.ChannelId);

		// Act & Assert - Channel ID format
		slack.ChannelId = "C1234567890";
		Assert.Equal("C1234567890", slack.ChannelId);
		Assert.StartsWith("C", slack.ChannelId);

		// Act & Assert - DM ID format
		slack.ChannelId = "D1234567890";
		Assert.Equal("D1234567890", slack.ChannelId);
		Assert.StartsWith("D", slack.ChannelId);

		// Act & Assert - User mention format
		slack.ChannelId = "@username";
		Assert.Equal("@username", slack.ChannelId);
		Assert.StartsWith("@", slack.ChannelId);
	}

	[Fact]
	public void Slack_BooleanFlagsWorkIndependently()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act
		slack.EnableInteractiveBot = true;
		slack.Enabled = false;

		// Assert
		Assert.True(slack.EnableInteractiveBot);
		Assert.False(slack.Enabled);
	}

	[Fact]
	public void Slack_CanToggleBooleanProperties()
	{
		// Arrange
		var slack = new Aula.Configuration.Slack();

		// Act & Assert - Toggle EnableInteractiveBot
		Assert.False(slack.EnableInteractiveBot);
		slack.EnableInteractiveBot = true;
		Assert.True(slack.EnableInteractiveBot);
		slack.EnableInteractiveBot = false;
		Assert.False(slack.EnableInteractiveBot);

		// Act & Assert - Toggle Enabled
		Assert.True(slack.Enabled);
		slack.Enabled = false;
		Assert.False(slack.Enabled);
		slack.Enabled = true;
		Assert.True(slack.Enabled);
	}
}
