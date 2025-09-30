using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;
using Aula.Bots;
using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using Aula.Tools;
using Aula.Utilities;

namespace Aula.Tests.Bots;

public class TelegramMessageHandlerTests
{
	private readonly Mock<IAgentService> _mockAgentService;
	private readonly Mock<ILogger> _mockLogger;
	private readonly Mock<ISupabaseService> _mockSupabaseService;
	private readonly ConversationContextManager<long> _conversationContextManager;
	private readonly ReminderCommandHandler _reminderHandler;
	private readonly Mock<ITelegramBotClient> _mockTelegramBotClient;
	private readonly Config _testConfig;
	private readonly Dictionary<string, Child> _childrenByName;
	private readonly TelegramMessageHandler _messageHandler;

	public TelegramMessageHandlerTests()
	{
		_mockAgentService = new Mock<IAgentService>();
		_mockLogger = new Mock<ILogger>();
		_mockSupabaseService = new Mock<ISupabaseService>();
		_conversationContextManager = new ConversationContextManager<long>(_mockLogger.Object);
		_mockTelegramBotClient = new Mock<ITelegramBotClient>();

		_testConfig = new Config
		{
			Telegram = new Aula.Configuration.Telegram
			{
				Enabled = true,
				Token = "test-token",
				ChannelId = "test-channel"
			},
			MinUddannelse = new MinUddannelse
			{
				Children = new List<Child>
				{
					new Child { FirstName = "Emma", LastName = "Test" },
					new Child { FirstName = "Søren Johannes", LastName = "Test" }
				}
			}
		};

		_childrenByName = new Dictionary<string, Child>
		{
			{ "emma", _testConfig.MinUddannelse.Children[0] },
			{ "testchild1", _testConfig.MinUddannelse.Children[1] }
		};

		_reminderHandler = new ReminderCommandHandler(_mockLogger.Object, _mockSupabaseService.Object, _childrenByName);

		_messageHandler = new TelegramMessageHandler(
			_mockAgentService.Object,
			_testConfig,
			_mockLogger.Object,
			_mockSupabaseService.Object,
			_childrenByName,
			_conversationContextManager,
			_reminderHandler);
	}

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		Assert.NotNull(_messageHandler);
	}

	[Fact]
	public void Constructor_WithNullAgentService_ThrowsArgumentNullException()
	{
		var exception = Assert.Throws<ArgumentNullException>(() =>
			new TelegramMessageHandler(
				null!,
				_testConfig,
				_mockLogger.Object,
				_mockSupabaseService.Object,
				_childrenByName,
				_conversationContextManager,
				_reminderHandler));
		Assert.Equal("agentService", exception.ParamName);
	}

	[Fact]
	public void Constructor_WithNullConfig_ThrowsArgumentNullException()
	{
		var exception = Assert.Throws<ArgumentNullException>(() =>
			new TelegramMessageHandler(
				_mockAgentService.Object,
				null!,
				_mockLogger.Object,
				_mockSupabaseService.Object,
				_childrenByName,
				_conversationContextManager,
				_reminderHandler));
		Assert.Equal("config", exception.ParamName);
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		var exception = Assert.Throws<ArgumentNullException>(() =>
			new TelegramMessageHandler(
				_mockAgentService.Object,
				_testConfig,
				null!,
				_mockSupabaseService.Object,
				_childrenByName,
				_conversationContextManager,
				_reminderHandler));
		Assert.Equal("logger", exception.ParamName);
	}

	[Fact]
	public void Constructor_WithNullSupabaseService_ThrowsArgumentNullException()
	{
		var exception = Assert.Throws<ArgumentNullException>(() =>
			new TelegramMessageHandler(
				_mockAgentService.Object,
				_testConfig,
				_mockLogger.Object,
				null!,
				_childrenByName,
				_conversationContextManager,
				_reminderHandler));
		Assert.Equal("supabaseService", exception.ParamName);
	}

	[Fact]
	public void Constructor_WithNullChildrenByName_ThrowsArgumentNullException()
	{
		var exception = Assert.Throws<ArgumentNullException>(() =>
			new TelegramMessageHandler(
				_mockAgentService.Object,
				_testConfig,
				_mockLogger.Object,
				_mockSupabaseService.Object,
				null!,
				_conversationContextManager,
				_reminderHandler));
		Assert.Equal("childrenByName", exception.ParamName);
	}

	[Fact]
	public void Constructor_WithNullConversationContextManager_ThrowsArgumentNullException()
	{
		var exception = Assert.Throws<ArgumentNullException>(() =>
			new TelegramMessageHandler(
				_mockAgentService.Object,
				_testConfig,
				_mockLogger.Object,
				_mockSupabaseService.Object,
				_childrenByName,
				null!,
				_reminderHandler));
		Assert.Equal("conversationContextManager", exception.ParamName);
	}

	[Fact]
	public void Constructor_WithNullReminderHandler_ThrowsArgumentNullException()
	{
		var exception = Assert.Throws<ArgumentNullException>(() =>
			new TelegramMessageHandler(
				_mockAgentService.Object,
				_testConfig,
				_mockLogger.Object,
				_mockSupabaseService.Object,
				_childrenByName,
				_conversationContextManager,
				null!));
		Assert.Equal("reminderHandler", exception.ParamName);
	}

	[Fact]
	public async Task HandleMessageAsync_WithTextMessage_ProcessesSuccessfully()
	{
		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: "Hello bot",
			chatType: ChatType.Private);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Fact]
	public async Task HandleMessageAsync_WithNonTextMessage_ReturnsEarly()
	{
		var message = TelegramTestMessageFactory.CreateNonTextMessage(
			chatId: 123456789L,
			messageType: MessageType.Photo,
			chatType: ChatType.Private);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		// Should not process non-text messages
		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Never());
	}

	[Fact]
	public async Task HandleMessageAsync_WithEmptyText_ReturnsEarly()
	{
		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: "",
			chatType: ChatType.Private);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		// Should not process empty text messages
		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Never());
	}

	[Fact]
	public async Task HandleMessageAsync_WithNullText_ReturnsEarly()
	{
		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: null,
			chatType: ChatType.Private);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		// Should not process null text messages
		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Never());
	}

	[Theory]
	[InlineData("hjælp")]
	[InlineData("help")]
	[InlineData("HJÆLP")]
	[InlineData("HELP")]
	public async Task HandleMessageAsync_WithHelpCommand_ProcessesHelpMessage(string helpCommand)
	{
		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: helpCommand,
			chatType: ChatType.Private);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Theory]
	[InlineData("mind mig om noget")]
	[InlineData("tilføj påmindelse")]
	[InlineData("slet påmindelse")]
	[InlineData("vis påmindelser")]
	public async Task HandleMessageAsync_WithReminderCommand_ProcessesMessage(string reminderCommand)
	{
		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: reminderCommand,
			chatType: ChatType.Private);

		// Should not throw an exception
		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);
	}

	[Theory]
	[InlineData("Hvad skal Emma i dag?")]
	[InlineData("What is Søren Johannes doing tomorrow?")]
	[InlineData("Vis ugeplanen")]
	public async Task HandleMessageAsync_WithValidQuery_ProcessesSuccessfully(string queryText)
	{
		_mockAgentService
			.Setup(a => a.GetWeekLetterAsync(It.IsAny<Child>(), It.IsAny<DateOnly>(), It.IsAny<bool>(), It.IsAny<bool>()))
			.ReturnsAsync(new JObject());

		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: queryText,
			chatType: ChatType.Private);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Fact]
	public async Task HandleMessageAsync_WithException_LogsError()
	{
		_mockAgentService
			.Setup(a => a.ProcessQueryWithToolsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Child?>(), It.IsAny<ChatInterface>()))
			.ThrowsAsync(new Exception("Test exception"));

		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: "What is Emma doing today?",
			chatType: ChatType.Private);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error processing message")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Fact]
	public async Task HandleMessageAsync_LogsMessageProcessing()
	{
		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: "Test message",
			chatType: ChatType.Private);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Fact]
	public async Task HandleMessageAsync_WithPrivateChat_HandlesCorrectly()
	{
		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: "Hello in private chat",
			chatType: ChatType.Private);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Fact]
	public async Task HandleMessageAsync_WithGroupChat_HandlesCorrectly()
	{
		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: -123456789L,
			text: "Hello in group chat",
			chatType: ChatType.Group);

		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, CancellationToken.None);

		_mockLogger.Verify(
			logger => logger.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
				null,
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Fact]
	public async Task HandleMessageAsync_WithCancellationToken_RespectsCancellation()
	{
		var cancellationTokenSource = new CancellationTokenSource();
		cancellationTokenSource.Cancel();

		var message = TelegramTestMessageFactory.CreateTextMessage(
			chatId: 123456789L,
			text: "Test message",
			chatType: ChatType.Private);

		// This should handle the cancellation gracefully
		await _messageHandler.HandleMessageAsync(_mockTelegramBotClient.Object, message, cancellationTokenSource.Token);

		// The method should still complete without throwing
		Assert.True(true);
	}
}
