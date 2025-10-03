using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace MinUddannelse.Content.Processing;

public class Html2TelegramConverter
{
    public string Convert(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        try
        {
            // Load the HTML document
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            if (htmlDoc.DocumentNode == null)
            {
                return CleanupFallback(html);
            }

            // Convert to Telegram-compatible HTML
            var result = ProcessNode(htmlDoc.DocumentNode);

            // Clean up extra whitespace but preserve paragraph breaks
            result = Regex.Replace(result, @"[ \t]+", " "); // Only collapse spaces and tabs, not newlines
            result = Regex.Replace(result, @"\n{3,}", "\n\n"); // Limit consecutive newlines to max 2
            result = Regex.Replace(result, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            result = result.Replace("&nbsp;", " ");

            return result.Trim();
        }
        catch (Exception)
        {
            // If HTML parsing fails, return cleaned text
            return CleanupFallback(html);
        }
    }

    private string ProcessNode(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            // Properly decode HTML entities like &aelig; and &oslash;
            return WebUtility.HtmlDecode(node.InnerText);
        }

        if (node.NodeType != HtmlNodeType.Element && node.NodeType != HtmlNodeType.Document)
        {
            return string.Empty;
        }

        var content = string.Join("", node.ChildNodes.Select(ProcessNode));

        return node.Name.ToLower() switch
        {
            "#document" => content, // Document node - just return content
            "html" => content, // HTML root - just return content
            "body" => content, // Body - just return content
            "b" or "strong" => $"<b>{content}</b>",
            "i" or "em" => $"<i>{content}</i>",
            "u" => $"<u>{content}</u>",
            "p" => $"{content}\n\n",
            "br" => "\n",
            "div" => $"{content}\n",
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => $"<b>{content}</b>\n\n",
            _ => content
        };
    }

    private string CleanupFallback(string html)
    {
        // Strip all HTML tags and clean up text
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ");
        text = text.Replace("&nbsp;", " ");
        return text.Trim();
    }
}