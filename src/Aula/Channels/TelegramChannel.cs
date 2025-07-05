using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aula.Configuration;
using Aula.Bots;

namespace Aula.Channels;

/// <summary>
/// Telegram channel implementation with support for HTML formatting and Telegram-specific features.
/// </summary>
public class TelegramChannel : IChannel
{
    private readonly ILogger _logger;
    private readonly Config _config;
    private readonly TelegramInteractiveBot? _bot;
    private readonly IChannelMessenger _messenger;

    public string PlatformId => "telegram";
    public string DisplayName => "Telegram";
    public bool IsEnabled => _config.Telegram.Enabled && !string.IsNullOrEmpty(_config.Telegram.Token);
    public bool SupportsInteractivity => IsEnabled && _bot != null;

    public ChannelCapabilities Capabilities { get; }

    public TelegramChannel(Config config, ILoggerFactory loggerFactory, IChannelMessenger messenger, TelegramInteractiveBot? bot = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = loggerFactory?.CreateLogger<TelegramChannel>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        _bot = bot;
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        Capabilities = new ChannelCapabilities
        {
            SupportsBold = true,
            SupportsItalic = true,
            SupportsCode = true,
            SupportsCodeBlocks = true,
            SupportsLinks = true,
            SupportsButtons = true,
            SupportsImages = true,
            SupportsFiles = true,
            SupportsThreads = false,
            SupportsEmojis = true,
            MaxMessageLength = 4096,
            SupportedFormatTags = new[] { "<b>bold</b>", "<i>italic</i>", "<code>code</code>", "<pre>codeblock</pre>", "<a href=\"url\">link</a>" }
        };
    }

    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Attempted to send empty message to Telegram");
            return;
        }

        try
        {
            await _messenger.SendMessageAsync(message);
            _logger.LogDebug("Successfully sent message to Telegram default channel");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Telegram default channel");
            throw;
        }
    }

    public async Task SendMessageAsync(string channelId, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Attempted to send empty message to Telegram channel: {ChannelId}", channelId);
            return;
        }

        try
        {
            await _messenger.SendMessageAsync(channelId, message);
            _logger.LogDebug("Successfully sent message to Telegram channel: {ChannelId}", channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Telegram channel: {ChannelId}", channelId);
            throw;
        }
    }

    public string FormatMessage(string message, MessageFormat format = MessageFormat.Auto)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        return format switch
        {
            MessageFormat.Platform or MessageFormat.Html => FormatForTelegram(message),
            MessageFormat.Markdown => ConvertMarkdownToHtml(message),
            MessageFormat.Plain => StripFormatting(message),
            MessageFormat.Auto => DetectAndFormat(message),
            _ => message
        };
    }

    public string? GetDefaultChannelId()
    {
        return _config.Telegram.ChannelId;
    }

    public Task<bool> TestConnectionAsync()
    {
        try
        {
            // Test with bot if available
            if (_bot != null && SupportsInteractivity)
            {
                // Could implement a getMe API call here
                return Task.FromResult(true);
            }

            // For basic setup, just check if token is configured
            return Task.FromResult(!string.IsNullOrEmpty(_config.Telegram.Token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram connection test failed");
            return Task.FromResult(false);
        }
    }

    public Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Telegram channel");

            if (_bot != null && SupportsInteractivity)
            {
                // Bot initialization is handled by the bot itself
                _logger.LogDebug("Telegram interactive bot available for initialization");
            }

            _logger.LogInformation("Telegram channel initialized successfully");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Telegram channel");
            throw;
        }
    }

    public async Task StartAsync()
    {
        try
        {
            if (_bot != null && SupportsInteractivity)
            {
                await _bot.Start();
                _logger.LogInformation("Started Telegram interactive bot");
            }
            else
            {
                _logger.LogInformation("Telegram channel ready (basic mode)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Telegram channel");
            throw;
        }
    }

    public Task StopAsync()
    {
        try
        {
            if (_bot != null && SupportsInteractivity)
            {
                _bot.Stop();
                _logger.LogInformation("Stopped Telegram interactive bot");
            }
            else
            {
                _logger.LogInformation("Telegram channel stopped (basic mode)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Telegram channel");
            throw;
        }

        return Task.CompletedTask;
    }

    private string FormatForTelegram(string message)
    {
        // Telegram supports HTML formatting
        // Ensure the message uses valid HTML tags that Telegram supports
        var formatted = message;

        // Escape any unescaped special characters
        formatted = EscapeSpecialCharacters(formatted);

        return formatted;
    }

    private string ConvertMarkdownToHtml(string markdownMessage)
    {
        var converted = markdownMessage;

        // Convert markdown to HTML for Telegram
        // Bold: **text** -> <b>text</b>
        converted = Regex.Replace(converted, @"\*\*(.*?)\*\*", "<b>$1</b>", RegexOptions.Singleline);

        // Italic: *text* -> <i>text</i>
        converted = Regex.Replace(converted, @"(?<!\*)\*([^*]+?)\*(?!\*)", "<i>$1</i>", RegexOptions.Singleline);

        // Code: `text` -> <code>text</code>
        converted = Regex.Replace(converted, @"`([^`]+?)`", "<code>$1</code>", RegexOptions.Singleline);

        // Code block: ```text``` -> <pre>text</pre>
        converted = Regex.Replace(converted, @"```(.*?)```", "<pre>$1</pre>", RegexOptions.Singleline);

        // Links: [text](url) -> <a href="url">text</a>
        converted = Regex.Replace(converted, @"\[([^\]]+?)\]\(([^)]+?)\)", "<a href=\"$2\">$1</a>", RegexOptions.Singleline);

        return converted;
    }

    private string StripFormatting(string message)
    {
        // Remove HTML formatting
        var stripped = message;
        stripped = Regex.Replace(stripped, @"<b>(.*?)</b>", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        stripped = Regex.Replace(stripped, @"<i>(.*?)</i>", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        stripped = Regex.Replace(stripped, @"<code>(.*?)</code>", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        stripped = Regex.Replace(stripped, @"<pre>(.*?)</pre>", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        stripped = Regex.Replace(stripped, @"<a[^>]*>(.*?)</a>", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        stripped = Regex.Replace(stripped, @"<[^>]+>", "", RegexOptions.Singleline);

        return stripped;
    }

    private static readonly Regex MarkdownPattern = new(@"(\*\*.*?\*\*)|(\*.*?\*)|(__.*?__)|(_.*?_)|(`.*?`)|(`{3}.*?`{3})|(\[.*?\]\(.*?\))", 
        RegexOptions.Singleline | RegexOptions.Compiled);
    
    private static readonly Regex HtmlTagPattern = new(@"<\s*\/?\s*(?:b|strong|i|em|code|pre|a)\s*(?:\s[^>]*)?\s*>", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string DetectAndFormat(string message)
    {
        // Enhanced markdown detection using regex patterns
        // Check for common markdown patterns: **bold**, *italic*, `code`, ```blocks```, [links](url)
        if (MarkdownPattern.IsMatch(message))
        {
            return ConvertMarkdownToHtml(message);
        }

        // Enhanced HTML detection using regex patterns
        if (HtmlTagPattern.IsMatch(message))
        {
            return FormatForTelegram(message);
        }

        return message;
    }

    private string EscapeSpecialCharacters(string message)
    {
        // Escape characters that have special meaning in Telegram HTML
        // but preserve valid HTML tags
        var escaped = message;

        // Only escape < and > that are not part of valid HTML tags
        // This is a simple approach - for production, consider using a proper HTML parser
        escaped = Regex.Replace(escaped, @"<(?!(?:b|i|/|code|pre|a\s|/b|/i|/code|/pre|/a)(?:\s|>))", "&lt;");
        escaped = Regex.Replace(escaped, @"(?<!<[^>]*)>(?![^<]*>)", "&gt;");

        return escaped;
    }
}