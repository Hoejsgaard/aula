using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aula.Configuration;
using Aula.Bots;

namespace Aula.Channels;

/// <summary>
/// Slack channel implementation with support for Slack-specific formatting and features.
/// </summary>
public class SlackChannel : IChannel
{
    private readonly ILogger _logger;
    private readonly Config _config;
    private readonly SlackInteractiveBot? _bot;
    private readonly IChannelMessenger _messenger;

    public string PlatformId => "slack";
    public string DisplayName => "Slack";
    public bool IsEnabled => _config.Slack.WebhookUrl != null || _config.Slack.EnableInteractiveBot;
    public bool SupportsInteractivity => _config.Slack.EnableInteractiveBot && _bot != null;

    public ChannelCapabilities Capabilities { get; }

    public SlackChannel(Config config, ILoggerFactory loggerFactory, SlackInteractiveBot? bot = null, IChannelMessenger? messenger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = loggerFactory?.CreateLogger<SlackChannel>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        _bot = bot;
        _messenger = messenger ?? new SlackChannelMessenger(new HttpClient(), config, loggerFactory);

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
            SupportsThreads = true,
            SupportsEmojis = true,
            MaxMessageLength = 4000,
            SupportedFormatTags = new[] { "*bold*", "_italic_", "`code`", "```codeblock```", "<link>" }
        };
    }

    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Attempted to send empty message to Slack");
            return;
        }

        try
        {
            await _messenger.SendMessageAsync(message);
            _logger.LogDebug("Successfully sent message to Slack default channel");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Slack default channel");
            throw;
        }
    }

    public async Task SendMessageAsync(string channelId, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Attempted to send empty message to Slack channel: {ChannelId}", channelId);
            return;
        }

        try
        {
            await _messenger.SendMessageAsync(channelId, message);
            _logger.LogDebug("Successfully sent message to Slack channel: {ChannelId}", channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Slack channel: {ChannelId}", channelId);
            throw;
        }
    }

    public string FormatMessage(string message, MessageFormat format = MessageFormat.Auto)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        return format switch
        {
            MessageFormat.Platform or MessageFormat.Markdown => FormatForSlack(message),
            MessageFormat.Html => ConvertHtmlToSlack(message),
            MessageFormat.Plain => StripFormatting(message),
            MessageFormat.Auto => DetectAndFormat(message),
            _ => message
        };
    }

    public string? GetDefaultChannelId()
    {
        return _config.Slack.ChannelId;
    }

    public Task<bool> TestConnectionAsync()
    {
        try
        {
            // Test with a simple ping message if interactive bot is available
            if (_bot != null && SupportsInteractivity)
            {
                // Could implement a health check here
                return Task.FromResult(true);
            }

            // For webhook-only, we can't easily test without sending a message
            // So we just check if the webhook URL is configured
            return Task.FromResult(!string.IsNullOrEmpty(_config.Slack.WebhookUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slack connection test failed");
            return Task.FromResult(false);
        }
    }

    public Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Slack channel");

            if (_bot != null && SupportsInteractivity)
            {
                // Bot initialization is handled by the bot itself
                _logger.LogDebug("Slack interactive bot available for initialization");
            }

            _logger.LogInformation("Slack channel initialized successfully");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Slack channel");
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
                _logger.LogInformation("Started Slack interactive bot");
            }
            else
            {
                _logger.LogInformation("Slack channel ready (webhook mode)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Slack channel");
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
                _logger.LogInformation("Stopped Slack interactive bot");
            }
            else
            {
                _logger.LogInformation("Slack channel stopped (webhook mode)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Slack channel");
            throw;
        }

        return Task.CompletedTask;
    }

    private string FormatForSlack(string message)
    {
        // Slack uses its own markdown-like formatting
        // Convert common markdown to Slack format
        var formatted = message;

        // Bold: **text** or __text__ -> *text*
        formatted = Regex.Replace(formatted, @"\*\*(.*?)\*\*", "*$1*", RegexOptions.Singleline);
        formatted = Regex.Replace(formatted, @"__(.*?)__", "*$1*", RegexOptions.Singleline);

        // Italic: *text* or _text_ -> _text_
        formatted = Regex.Replace(formatted, @"(?<!\*)\*([^*]+?)\*(?!\*)", "_$1_", RegexOptions.Singleline);

        return formatted;
    }

    private string ConvertHtmlToSlack(string htmlMessage)
    {
        var converted = htmlMessage;

        // Convert common HTML tags to Slack format
        converted = Regex.Replace(converted, @"<b>(.*?)</b>", "*$1*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        converted = Regex.Replace(converted, @"<strong>(.*?)</strong>", "*$1*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        converted = Regex.Replace(converted, @"<i>(.*?)</i>", "_$1_", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        converted = Regex.Replace(converted, @"<em>(.*?)</em>", "_$1_", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        converted = Regex.Replace(converted, @"<code>(.*?)</code>", "`$1`", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        converted = Regex.Replace(converted, @"<pre>(.*?)</pre>", "```$1```", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        converted = Regex.Replace(converted, @"<br\s*/?>\s*", "\n", RegexOptions.IgnoreCase);

        // Remove any remaining HTML tags
        converted = Regex.Replace(converted, @"<[^>]+>", "", RegexOptions.Singleline);

        return converted;
    }

    private string StripFormatting(string message)
    {
        // Remove Slack formatting
        var stripped = message;
        stripped = Regex.Replace(stripped, @"\*([^*]+?)\*", "$1", RegexOptions.Singleline); // Bold
        stripped = Regex.Replace(stripped, @"_([^_]+?)_", "$1", RegexOptions.Singleline);   // Italic
        stripped = Regex.Replace(stripped, @"`([^`]+?)`", "$1", RegexOptions.Singleline);   // Code
        stripped = Regex.Replace(stripped, @"```([^`]+?)```", "$1", RegexOptions.Singleline); // Code block

        return stripped;
    }

    private static readonly Regex HtmlTagPattern = new(@"<\s*\/?\s*(?:b|strong|i|em|code|pre|br|p|div|span)\s*(?:\s[^>]*)?\s*>", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex HtmlEntityPattern = new(@"&(?:amp|lt|gt|quot|apos|nbsp);", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string DetectAndFormat(string message)
    {
        // Enhanced HTML detection using regex patterns
        // Check for HTML tags or entities that indicate HTML content
        if (HtmlTagPattern.IsMatch(message) || HtmlEntityPattern.IsMatch(message))
        {
            return ConvertHtmlToSlack(message);
        }

        return FormatForSlack(message);
    }
}