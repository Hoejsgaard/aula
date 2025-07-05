using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Aula.Configuration;

namespace Aula.Channels;

/// <summary>
/// Telegram-specific implementation of IChannelMessenger that sends messages
/// via Telegram Bot API without depending on bot implementations.
/// </summary>
public class TelegramChannelMessenger : IChannelMessenger, IDisposable
{
    private readonly ITelegramBotClient _telegramClient;
    private readonly Config _config;
    private readonly ILogger _logger;

    public string PlatformType => "Telegram";

    public TelegramChannelMessenger(ITelegramBotClient telegramClient, Config config, ILoggerFactory loggerFactory)
    {
        _telegramClient = telegramClient ?? throw new ArgumentNullException(nameof(telegramClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<TelegramChannelMessenger>();
    }

    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(_config.Telegram.ChannelId))
        {
            throw new InvalidOperationException("Default Telegram channel ID is not configured");
        }

        await SendMessageAsync(_config.Telegram.ChannelId, message);
    }

    public async Task SendMessageAsync(string channelId, string message)
    {
        try
        {
            _logger.LogInformation("Sending Telegram message to chat {ChatId}: {MessageLength} characters", channelId, message.Length);

            await _telegramClient.SendTextMessageAsync(
                chatId: new ChatId(channelId),
                text: message,
                parseMode: ParseMode.Html
            );

            _logger.LogInformation("Telegram message sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Telegram message to chat {ChatId}", channelId);
            throw;
        }
    }

    public void Dispose()
    {
        if (_telegramClient is IDisposable disposableClient)
        {
            disposableClient.Dispose();
        }
    }
}