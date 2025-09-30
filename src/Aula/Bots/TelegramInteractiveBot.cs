using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Aula.Integration;
using Aula.Tools;
using Aula.Channels;
using Aula.Configuration;
using Aula.Services;
using Aula.Utilities;

namespace Aula.Bots;

public class TelegramInteractiveBot : IDisposable
{
	private readonly IAgentService _agentService;
	private readonly Config _config;
	private readonly ILogger _logger;
	private readonly ITelegramBotClient _telegramClient;
	private readonly TelegramChannel _telegramChannel;
	private readonly ISupabaseService _supabaseService;
	private readonly Dictionary<string, Child> _childrenByName;
	private readonly ConcurrentDictionary<string, byte> _postedWeekLetterHashes = new ConcurrentDictionary<string, byte>();
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly TelegramMessageHandler _messageHandler;
	// Language detection arrays removed - GPT handles language detection naturally


	public TelegramInteractiveBot(
		IAgentService agentService,
		Config config,
		ILoggerFactory loggerFactory,
		ISupabaseService supabaseService)
	{
		_agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<TelegramInteractiveBot>();
		_supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

		if (_config.Telegram.Enabled && !string.IsNullOrEmpty(_config.Telegram.Token))
		{
			_telegramClient = new TelegramBotClient(_config.Telegram.Token);
			var telegramMessenger = new TelegramChannelMessenger(_telegramClient, _config, loggerFactory);
			_telegramChannel = new TelegramChannel(_config, loggerFactory, telegramMessenger, this);
		}
		else
		{
			throw new InvalidOperationException("Telegram bot is not enabled or token is missing");
		}

		_childrenByName = _config.MinUddannelse.Children.ToDictionary(
			c => $"{c.FirstName} {c.LastName}".ToLowerInvariant(),
			c => c);

		var reminderHandler = new ReminderCommandHandler(_logger, _supabaseService, _childrenByName);
		var conversationContextManager = new ConversationContextManager<long>(_logger);
		_messageHandler = new TelegramMessageHandler(_agentService, _config, _logger, _supabaseService, _childrenByName, conversationContextManager, reminderHandler);
	}

	public async Task Start()
	{
		if (!_config.Telegram.Enabled)
		{
			_logger.LogError("Cannot start Telegram bot: Telegram integration is not enabled");
			return;
		}

		if (string.IsNullOrEmpty(_config.Telegram.Token))
		{
			_logger.LogError("Cannot start Telegram bot: Token is missing");
			return;
		}

		_logger.LogInformation("Starting Telegram interactive bot");

		// Create cancellation token source for graceful shutdown
		_cancellationTokenSource = new CancellationTokenSource();

		// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
		var receiverOptions = new ReceiverOptions
		{
			AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
		};

		// Register handlers for updates
		_telegramClient.StartReceiving(
			HandleUpdateAsync,
			HandlePollingErrorAsync,
			receiverOptions,
			_cancellationTokenSource.Token
		);

		// Build a list of available children (first names only)
		string childrenList = string.Join(" og ", _childrenByName.Values.Select(c => c.FirstName.Split(' ')[0]));

		// Get the current week number
		int weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);

		// Send welcome message in Danish with children info to the configured channel
		if (!string.IsNullOrEmpty(_config.Telegram.ChannelId))
		{
			await SendMessageToChannel($"Jeg er online og har ugeplan for {childrenList} for Uge {weekNumber}");
		}

		_logger.LogInformation("Telegram interactive bot started");

		// Keep the bot running until cancellation is requested
		try
		{
			await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("Telegram bot shutdown requested");
		}
	}

	public void Stop()
	{
		_logger.LogInformation("Stopping Telegram interactive bot");

		_cancellationTokenSource?.Cancel();
		_cancellationTokenSource?.Dispose();
		_cancellationTokenSource = null;

		_logger.LogInformation("Telegram interactive bot stopped");
	}

	private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
	{
		_logger.LogInformation("Received update: {UpdateType}", update.Type);

		// Only process message updates
		if (update.Message is not { } message)
		{
			_logger.LogWarning("Update does not contain a message");
			return;
		}

		_logger.LogInformation("Message from user: {FirstName} {LastName} (@{Username})",
			message.From?.FirstName ?? "Unknown",
			message.From?.LastName ?? "",
			message.From?.Username ?? "Unknown");
		_logger.LogInformation("Chat type: {ChatType}, Title: {Title}",
			message.Chat.Type,
			message.Chat.Title ?? "N/A");

		// Delegate to message handler
		await _messageHandler.HandleMessageAsync(botClient, message, cancellationToken);
	}

	private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
	{
		var errorMessage = exception switch
		{
			ApiRequestException apiRequestException
				=> $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
			_ => exception.ToString()
		};

		_logger.LogError(errorMessage);
		return Task.CompletedTask;
	}

	// Message handling logic moved to TelegramMessageHandler


	public async Task SendMessage(long chatId, string text)
	{
		await SendMessageInternal(chatId, text);
	}

	public async Task SendMessage(string chatId, string text)
	{
		await SendMessageInternal(chatId, text);
	}

	private async Task SendMessageInternal(string chatId, string text)
	{
		try
		{
			_logger.LogInformation("Sending message to chat {ChatId}: {TextLength} characters", chatId, text.Length);

			await _telegramClient.SendTextMessageAsync(
				chatId: new ChatId(chatId),
				text: text,
				parseMode: ParseMode.Html
			);

			_logger.LogInformation("Message sent successfully to chat {ChatId}", chatId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error sending message to chat {ChatId}", chatId);
		}
	}

	private async Task SendMessageInternal(long chatId, string text)
	{
		await SendMessageInternal(chatId.ToString(), text);
	}

	private async Task<bool> SendMessageToChannel(string message)
	{
		if (!_config.Telegram.Enabled || string.IsNullOrEmpty(_config.Telegram.ChannelId))
		{
			_logger.LogWarning("Telegram integration is disabled or channel ID is missing. Message not sent.");
			return false;
		}

		try
		{
			await _telegramClient.SendTextMessageAsync(
				chatId: new ChatId(_config.Telegram.ChannelId),
				text: message,
				parseMode: ParseMode.Html
			);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error sending message to channel");
		}

		return false;
	}

	public async Task PostWeekLetter(string childName, JObject weekLetter)
	{
		if (!_config.Telegram.Enabled || string.IsNullOrEmpty(_config.Telegram.ChannelId))
		{
			_logger.LogWarning("Telegram integration is disabled or channel ID is missing. Week letter not posted.");
			return;
		}

		try
		{
			// Find the child
			var child = _childrenByName.Values.FirstOrDefault(c =>
				c.FirstName.Equals(childName, StringComparison.OrdinalIgnoreCase));

			if (child == null)
			{
				_logger.LogWarning("Child not found for week letter posting: {ChildName}", childName);
				return;
			}

			// Create a hash of the week letter to avoid duplicates
			var weekLetterContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
			var hash = ComputeHash(weekLetterContent);

			// Check if we've already posted this week letter
			if (_postedWeekLetterHashes.ContainsKey(hash))
			{
				_logger.LogInformation("Week letter for {ChildName} already posted (hash: {Hash})", childName, hash);
				return;
			}

			// Post the week letter using the TelegramChannel
			try
			{
				// Extract the actual content from the JObject
				var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
				var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? "";
				var htmlContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";

				// Create formatted message
				var title = $"📚 **Ugebrev for {child.FirstName} ({@class}) uge {week}**";
				var message = $"{title}\n\n{htmlContent}";

				await _telegramChannel.SendMessageAsync(message);

				// Add the hash to avoid duplicates
				_postedWeekLetterHashes.TryAdd(hash, 0);
				_logger.LogInformation("Week letter for {ChildName} posted successfully", childName);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to post week letter for {ChildName}", childName);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error posting week letter for {ChildName}", childName);
		}
	}

	private string ComputeHash(string input)
	{
		using var sha256 = SHA256.Create();
		var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
		return Convert.ToBase64String(bytes);
	}

	public void Dispose()
	{
		Stop();
	}
}
