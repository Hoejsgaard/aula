using System.Diagnostics;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Requests;

namespace Aula;


public class TelegramClient
{
    private readonly Html2SlackMarkdownConverter _markdownConverter;
    private readonly ITelegramBotClient? _telegram;
    private readonly bool _enabled;

    public TelegramClient(Config config)
    {
        _enabled = config.Telegram.Enabled;
        if (_enabled)
        {
            _telegram = new TelegramBotClient(config.Telegram.Token);
        }
        _markdownConverter = new Html2SlackMarkdownConverter();
    }

    public TelegramClient(string token)
    {
        _enabled = true;
        _telegram = new TelegramBotClient(token);
        _markdownConverter = new Html2SlackMarkdownConverter();
    }

    public async Task<bool> SendMessageToChannel(string channelId, string message)
    {
        if (!_enabled)
        {
            Console.WriteLine("Telegram integration is disabled. Message not sent.");
            return false;
        }

        try
        {
            await _telegram!.SendTextMessageAsync(
                chatId: new ChatId(channelId),
                text: message,
                parseMode: ParseMode.Html
            );
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }

        return false;
    }

    public async Task<bool> PostWeekLetter(string channelId, JObject weekLetter, Child child)
    {
        if (!_enabled)
        {
            Console.WriteLine("Telegram integration is disabled. Week letter not posted.");
            return false;
        }

        var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
        var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? "";

        // Get the original HTML content
        var htmlContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";

        try
        {
            // Try to sanitize HTML for Telegram compatibility
            htmlContent = SanitizeHtmlForTelegram(htmlContent);

            // Create title
            var titleText = $"Ugebrev for {child.FirstName} ({@class}) uge {week}";

            // Format as HTML with proper br tag format
            var message = $"<b>{titleText}</b><br/><br/>{htmlContent}";

            return await SendMessageToChannel(channelId, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sanitizing HTML: {ex.Message}. Falling back to plain text.");

            // Fallback to plain text if HTML parsing fails
            var plainText = _markdownConverter.Convert(htmlContent).Replace("*", "").Replace("_", "");
            var titleText = $"Ugebrev for {child.FirstName} ({@class}) uge {week}";
            var message = $"{titleText}\n\n{plainText}";

            // Send without parse mode
            try
            {
                await _telegram!.SendTextMessageAsync(
                    new ChatId(channelId),
                    message
                );
                return true;
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"Error sending fallback message: {fallbackEx.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Sanitizes HTML to ensure compatibility with Telegram's HTML parser
    /// </summary>
    private string SanitizeHtmlForTelegram(string html)
    {
        // Telegram only supports a limited set of HTML tags:
        // <b>, <i>, <u>, <s>, <a>, <code>, <pre>, <tg-spoiler>
        // See: https://core.telegram.org/bots/api#html-style

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Replace <br> tags with <br/>
        var brTags = doc.DocumentNode.SelectNodes("//br");
        if (brTags != null)
        {
            foreach (var tag in brTags)
            {
                var parent = tag.ParentNode;
                if (parent != null)
                {
                    parent.ReplaceChild(HtmlNode.CreateNode("<br/>"), tag);
                }
            }
        }

        // Replace <div> tags with line breaks
        var divTags = doc.DocumentNode.SelectNodes("//div");
        if (divTags != null)
        {
            foreach (var tag in divTags)
            {
                // Create a new text node with the inner HTML
                var innerContent = tag.InnerHtml;
                var parent = tag.ParentNode;
                if (parent != null)
                {
                    // First insert the content
                    var contentNode = HtmlNode.CreateNode(innerContent);
                    parent.ReplaceChild(contentNode, tag);

                    // Then add a line break after it
                    var brNode = HtmlNode.CreateNode("<br/>");
                    parent.InsertAfter(brNode, contentNode);
                }
            }
        }

        // Replace <p> tags with line breaks
        var pTags = doc.DocumentNode.SelectNodes("//p");
        if (pTags != null)
        {
            foreach (var tag in pTags)
            {
                // Create a new text node with the inner HTML
                var innerContent = tag.InnerHtml;
                var parent = tag.ParentNode;
                if (parent != null)
                {
                    // First insert the content
                    var contentNode = HtmlNode.CreateNode(innerContent);
                    parent.ReplaceChild(contentNode, tag);

                    // Then add two line breaks after it
                    var br1 = HtmlNode.CreateNode("<br/>");
                    parent.InsertAfter(br1, contentNode);
                    var br2 = HtmlNode.CreateNode("<br/>");
                    parent.InsertAfter(br2, br1);
                }
            }
        }

        // Convert <strong> to <b>
        var strongTags = doc.DocumentNode.SelectNodes("//strong");
        if (strongTags != null)
        {
            foreach (var tag in strongTags)
            {
                tag.Name = "b";
            }
        }

        // Convert <em> to <i>
        var emTags = doc.DocumentNode.SelectNodes("//em");
        if (emTags != null)
        {
            foreach (var tag in emTags)
            {
                tag.Name = "i";
            }
        }

        // Remove all other unsupported tags but keep their content
        var allNodes = doc.DocumentNode.SelectNodes("//*");
        if (allNodes != null)
        {
            var supportedTags = new HashSet<string> { "b", "i", "u", "s", "a", "code", "pre", "br" };

            // Process nodes from bottom to top to avoid modifying the structure while iterating
            foreach (var node in allNodes.OrderByDescending(n => n.StreamPosition).ToList())
            {
                if (!supportedTags.Contains(node.Name.ToLower()))
                {
                    // Keep the inner HTML content
                    var content = node.InnerHtml;

                    // Create a text node with the inner HTML
                    var textNode = HtmlNode.CreateNode(content);

                    // Replace the node with its inner HTML
                    var parent = node.ParentNode;
                    if (parent != null)
                    {
                        parent.ReplaceChild(textNode, node);
                    }
                }
            }
        }

        // Get the sanitized HTML
        var sanitizedHtml = doc.DocumentNode.InnerHtml;

        // Fix common HTML entities
        sanitizedHtml = sanitizedHtml
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">");

        return sanitizedHtml;
    }
}