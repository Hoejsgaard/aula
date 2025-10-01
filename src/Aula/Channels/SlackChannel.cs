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
public partial class SlackChannel : IChannel
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

        if (messenger != null && messenger is not SlackChannelMessenger)
        {
            throw new ArgumentException("Messenger must be a SlackChannelMessenger or null", nameof(messenger));
        }

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
                // Bot lifecycle is managed by ChildAgent, not by the channel
                _logger.LogInformation("Slack channel ready with interactive bot");
            }
            else
            {
                _logger.LogInformation("Slack channel ready (webhook mode)");
            }

            await Task.CompletedTask;
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
                // Bot lifecycle is managed by ChildAgent, not by the channel
                _logger.LogInformation("Slack channel stopped (interactive bot managed by ChildAgent)");
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

        // Process in order to avoid conflicts:
        // 1. First convert double asterisks (bold) and double underscores (bold) to single asterisks
        formatted = DoubleBoldPattern().Replace(formatted, "BOLD_PLACEHOLDER_$1_PLACEHOLDER");
        formatted = DoubleUnderscorePattern().Replace(formatted, "BOLD_PLACEHOLDER_$1_PLACEHOLDER");

        // 2. Then convert remaining single asterisks to underscores (italic)
        formatted = SingleAsteriskPattern().Replace(formatted, "_$1_");

        // 3. Finally replace placeholders with actual Slack bold format
        formatted = BoldPlaceholderPattern().Replace(formatted, "*$1*");

        return formatted;
    }

    private string ConvertHtmlToSlack(string htmlMessage)
    {
        var converted = htmlMessage;

        // Convert common HTML tags to Slack format
        converted = HtmlBoldPattern().Replace(converted, "*$1*");
        converted = HtmlStrongPattern().Replace(converted, "*$1*");
        converted = HtmlItalicPattern().Replace(converted, "_$1_");
        converted = HtmlEmphasisPattern().Replace(converted, "_$1_");
        converted = HtmlCodePattern().Replace(converted, "`$1`");
        converted = HtmlPrePattern().Replace(converted, "```$1```");
        converted = HtmlBreakPattern().Replace(converted, "\n");

        // Remove any remaining HTML tags
        converted = AnyHtmlTagPattern().Replace(converted, "");

        return converted;
    }

    private string StripFormatting(string message)
    {
        // Remove Slack formatting - process longer patterns first
        var stripped = message;
        stripped = CodeBlockPattern().Replace(stripped, "$1"); // Code block (3 backticks)
        stripped = CodeSpanPattern().Replace(stripped, "$1");   // Code (1 backtick)
        stripped = SingleAsteriskPattern().Replace(stripped, "$1"); // Bold
        stripped = UnderscoreItalicPattern().Replace(stripped, "$1");   // Italic

        return stripped;
    }

    [GeneratedRegex(@"<\s*\/?\s*(?:b|strong|i|em|code|pre|br|p|div|span)\s*[^>]*\s*\/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"&(?:amp|lt|gt|quot|apos|nbsp);", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlEntityPattern();

    // Slack formatting patterns
    [GeneratedRegex(@"\*\*(.*?)\*\*", RegexOptions.Singleline)]
    private static partial Regex DoubleBoldPattern();

    [GeneratedRegex(@"__(.*?)__", RegexOptions.Singleline)]
    private static partial Regex DoubleUnderscorePattern();

    [GeneratedRegex(@"\*([^*]+?)\*", RegexOptions.Singleline)]
    private static partial Regex SingleAsteriskPattern();

    [GeneratedRegex(@"BOLD_PLACEHOLDER_(.*?)_PLACEHOLDER", RegexOptions.Singleline)]
    private static partial Regex BoldPlaceholderPattern();

    // HTML conversion patterns
    [GeneratedRegex(@"<b>(.*?)</b>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlBoldPattern();

    [GeneratedRegex(@"<strong>(.*?)</strong>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlStrongPattern();

    [GeneratedRegex(@"<i>(.*?)</i>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlItalicPattern();

    [GeneratedRegex(@"<em>(.*?)</em>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlEmphasisPattern();

    [GeneratedRegex(@"<code>(.*?)</code>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlCodePattern();

    [GeneratedRegex(@"<pre>(.*?)</pre>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlPrePattern();

    [GeneratedRegex(@"<br\s*/?>\s*", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlBreakPattern();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex AnyHtmlTagPattern();

    // Strip formatting patterns
    [GeneratedRegex(@"```(.*?)```", RegexOptions.Singleline)]
    private static partial Regex CodeBlockPattern();

    [GeneratedRegex(@"`([^`]+?)`", RegexOptions.Singleline)]
    private static partial Regex CodeSpanPattern();

    [GeneratedRegex(@"_([^_]+?)_", RegexOptions.Singleline)]
    private static partial Regex UnderscoreItalicPattern();

    private string DetectAndFormat(string message)
    {
        // Enhanced HTML detection using regex patterns
        // Check for HTML tags or entities that indicate HTML content
        if (HtmlTagPattern().IsMatch(message) || HtmlEntityPattern().IsMatch(message))
        {
            return ConvertHtmlToSlack(message);
        }

        return FormatForSlack(message);
    }
}
