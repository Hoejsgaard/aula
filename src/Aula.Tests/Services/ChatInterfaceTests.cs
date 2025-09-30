using Aula.Services;
using Xunit;

namespace Aula.Tests.Services;

public class ChatInterfaceTests
{
	[Fact]
	public void ChatInterface_IsEnum()
	{
		// Arrange
		var type = typeof(ChatInterface);

		// Act & Assert
		Assert.True(type.IsEnum);
		Assert.True(type.IsPublic);
		Assert.Equal("Aula.Services", type.Namespace);
	}

	[Fact]
	public void ChatInterface_HasExpectedValues()
	{
		// Act
		var values = Enum.GetValues<ChatInterface>();

		// Assert
		Assert.Contains(ChatInterface.Slack, values);
		Assert.Contains(ChatInterface.Telegram, values);
		Assert.Equal(2, values.Length);
	}

	[Fact]
	public void ChatInterface_HasExpectedNames()
	{
		// Act
		var names = Enum.GetNames<ChatInterface>();

		// Assert
		Assert.Contains("Slack", names);
		Assert.Contains("Telegram", names);
		Assert.Equal(2, names.Length);
	}

	[Fact]
	public void ChatInterface_HasCorrectUnderlyingType()
	{
		// Arrange
		var type = typeof(ChatInterface);

		// Act
		var underlyingType = Enum.GetUnderlyingType(type);

		// Assert
		Assert.Equal(typeof(int), underlyingType);
	}

	[Theory]
	[InlineData(ChatInterface.Slack, 0)]
	[InlineData(ChatInterface.Telegram, 1)]
	public void ChatInterface_HasExpectedNumericValues(ChatInterface chatInterface, int expectedValue)
	{
		// Act
		var actualValue = (int)chatInterface;

		// Assert
		Assert.Equal(expectedValue, actualValue);
	}

	[Fact]
	public void ChatInterface_Slack_HasCorrectStringRepresentation()
	{
		// Act
		var result = ChatInterface.Slack.ToString();

		// Assert
		Assert.Equal("Slack", result);
	}

	[Fact]
	public void ChatInterface_Telegram_HasCorrectStringRepresentation()
	{
		// Act
		var result = ChatInterface.Telegram.ToString();

		// Assert
		Assert.Equal("Telegram", result);
	}

	[Theory]
	[InlineData("Slack", ChatInterface.Slack)]
	[InlineData("Telegram", ChatInterface.Telegram)]
	public void ChatInterface_CanParseFromString(string input, ChatInterface expected)
	{
		// Act
		var result = Enum.Parse<ChatInterface>(input);

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("slack", ChatInterface.Slack)]
	[InlineData("telegram", ChatInterface.Telegram)]
	[InlineData("SLACK", ChatInterface.Slack)]
	[InlineData("TELEGRAM", ChatInterface.Telegram)]
	public void ChatInterface_CanParseFromStringIgnoreCase(string input, ChatInterface expected)
	{
		// Act
		var result = Enum.Parse<ChatInterface>(input, ignoreCase: true);

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("Slack", true)]
	[InlineData("Telegram", true)]
	[InlineData("slack", false)]
	[InlineData("telegram", false)]
	[InlineData("Discord", false)]
	[InlineData("", false)]
	[InlineData("  ", false)]
	public void ChatInterface_TryParseReturnsCorrectResult(string input, bool expectedSuccess)
	{
		// Act
		var success = Enum.TryParse<ChatInterface>(input, out var result);

		// Assert
		Assert.Equal(expectedSuccess, success);
		if (expectedSuccess)
		{
			Assert.True(Enum.IsDefined(typeof(ChatInterface), result));
		}
	}

	[Theory]
	[InlineData("slack", true)]
	[InlineData("telegram", true)]
	[InlineData("SLACK", true)]
	[InlineData("TELEGRAM", true)]
	[InlineData("Discord", false)]
	[InlineData("WhatsApp", false)]
	public void ChatInterface_TryParseIgnoreCaseReturnsCorrectResult(string input, bool expectedSuccess)
	{
		// Act
		var success = Enum.TryParse<ChatInterface>(input, ignoreCase: true, out var result);

		// Assert
		Assert.Equal(expectedSuccess, success);
		if (expectedSuccess)
		{
			Assert.True(Enum.IsDefined(typeof(ChatInterface), result));
		}
	}

	[Fact]
	public void ChatInterface_CanBeUsedInSwitchExpression()
	{
		// Arrange & Act
		var slackResult = GetInterfaceDescription(ChatInterface.Slack);
		var telegramResult = GetInterfaceDescription(ChatInterface.Telegram);

		// Assert
		Assert.Equal("Slack messaging platform", slackResult);
		Assert.Equal("Telegram messaging platform", telegramResult);
	}

	[Fact]
	public void ChatInterface_CanBeCompared()
	{
		// Act & Assert
#pragma warning disable CS1718 // Comparison made to same variable (intentional for testing equality operators)
		Assert.True(ChatInterface.Slack == ChatInterface.Slack);
		Assert.True(ChatInterface.Telegram == ChatInterface.Telegram);
#pragma warning restore CS1718
		Assert.False(ChatInterface.Slack == ChatInterface.Telegram);
		Assert.False(ChatInterface.Telegram == ChatInterface.Slack);

#pragma warning disable CS1718 // Comparison made to same variable (intentional for testing equality operators)
		Assert.False(ChatInterface.Slack != ChatInterface.Slack);
		Assert.False(ChatInterface.Telegram != ChatInterface.Telegram);
#pragma warning restore CS1718
		Assert.True(ChatInterface.Slack != ChatInterface.Telegram);
		Assert.True(ChatInterface.Telegram != ChatInterface.Slack);
	}

	[Fact]
	public void ChatInterface_CanBeUsedInDictionary()
	{
		// Arrange
		var interfaceDescriptions = new Dictionary<ChatInterface, string>
		{
			{ ChatInterface.Slack, "Team collaboration platform" },
			{ ChatInterface.Telegram, "Cloud-based messaging app" }
		};

		// Act & Assert
		Assert.Equal("Team collaboration platform", interfaceDescriptions[ChatInterface.Slack]);
		Assert.Equal("Cloud-based messaging app", interfaceDescriptions[ChatInterface.Telegram]);
		Assert.Equal(2, interfaceDescriptions.Count);
	}

	[Fact]
	public void ChatInterface_CanBeUsedInHashSet()
	{
		// Arrange
		var supportedInterfaces = new HashSet<ChatInterface> { ChatInterface.Slack, ChatInterface.Telegram };

		// Act & Assert
		Assert.Contains(ChatInterface.Slack, supportedInterfaces);
		Assert.Contains(ChatInterface.Telegram, supportedInterfaces);
		Assert.Equal(2, supportedInterfaces.Count);
	}

	[Fact]
	public void ChatInterface_CanBeConvertedToAndFromInt()
	{
		// Act
		var slackAsInt = (int)ChatInterface.Slack;
		var telegramAsInt = (int)ChatInterface.Telegram;
		var backToSlack = (ChatInterface)slackAsInt;
		var backToTelegram = (ChatInterface)telegramAsInt;

		// Assert
		Assert.Equal(0, slackAsInt);
		Assert.Equal(1, telegramAsInt);
		Assert.Equal(ChatInterface.Slack, backToSlack);
		Assert.Equal(ChatInterface.Telegram, backToTelegram);
	}

	[Fact]
	public void ChatInterface_IsDefined_WorksCorrectly()
	{
		// Act & Assert
		Assert.True(Enum.IsDefined(typeof(ChatInterface), ChatInterface.Slack));
		Assert.True(Enum.IsDefined(typeof(ChatInterface), ChatInterface.Telegram));
		Assert.True(Enum.IsDefined(typeof(ChatInterface), 0));
		Assert.True(Enum.IsDefined(typeof(ChatInterface), 1));
		Assert.False(Enum.IsDefined(typeof(ChatInterface), 2));
		Assert.False(Enum.IsDefined(typeof(ChatInterface), -1));
		Assert.False(Enum.IsDefined(typeof(ChatInterface), 999));
	}

	[Fact]
	public void ChatInterface_GetValues_ReturnsAllValues()
	{
		// Act
		var values = Enum.GetValues(typeof(ChatInterface)).Cast<ChatInterface>().ToArray();

		// Assert
		Assert.Equal(2, values.Length);
		Assert.Contains(ChatInterface.Slack, values);
		Assert.Contains(ChatInterface.Telegram, values);
	}

	[Fact]
	public void ChatInterface_GetNames_ReturnsAllNames()
	{
		// Act
		var names = Enum.GetNames(typeof(ChatInterface));

		// Assert
		Assert.Equal(2, names.Length);
		Assert.Contains("Slack", names);
		Assert.Contains("Telegram", names);
	}

	[Fact]
	public void ChatInterface_CanBeUsedAsGenericConstraint()
	{
		// Act & Assert
		var slackResult = ProcessChatInterface(ChatInterface.Slack);
		var telegramResult = ProcessChatInterface(ChatInterface.Telegram);

		Assert.Equal("Processing: Slack", slackResult);
		Assert.Equal("Processing: Telegram", telegramResult);
	}

	private static string GetInterfaceDescription(ChatInterface chatInterface)
	{
		return chatInterface switch
		{
			ChatInterface.Slack => "Slack messaging platform",
			ChatInterface.Telegram => "Telegram messaging platform",
			_ => "Unknown interface"
		};
	}

	private static string ProcessChatInterface<T>(T chatInterface) where T : Enum
	{
		return $"Processing: {chatInterface}";
	}
}
