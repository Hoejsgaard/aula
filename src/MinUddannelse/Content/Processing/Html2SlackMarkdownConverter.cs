using System.Text.RegularExpressions;
using MinUddannelse.Content.Processing;
using Html2Markdown;
using HtmlAgilityPack;
using System.Linq;

namespace MinUddannelse.Content.Processing;

public class Html2SlackMarkdownConverter
{
    private readonly Converter _converter;

    public Html2SlackMarkdownConverter()
    {
        _converter = new Converter();
    }
    public string Convert(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        try
        {
            var markdown = _converter.Convert(html).Replace("**", "*").Replace("\u00A0", " ").Replace("*   ", "- ");

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(markdown);

            CleanHtmlDocument(htmlDoc);

            var result = htmlDoc.DocumentNode?.InnerText ?? string.Empty;

            if (string.IsNullOrEmpty(result) || result.Trim().All(c => c == '<' || c == '>'))
            {
                return StripHtmlTags(html);
            }

            return result;
        }
        catch (Exception)
        {
            return StripHtmlTags(html);
        }
    }
    private void CleanHtmlDocument(HtmlDocument htmlDoc)
    {
        if (htmlDoc?.DocumentNode == null)
            return;

        RemoveNodesButKeepContent(htmlDoc, new[] { "span", "div" });
        RemoveNodes(htmlDoc, new[] { "style", "br" });

        var divNodes = htmlDoc.DocumentNode.SelectNodes("//div");
        if (divNodes != null)
        {
            foreach (var divNode in divNodes.ToList())
            {
                if (divNode.ParentNode == null) continue;

                var parentNode = divNode.ParentNode;
                var textNode = htmlDoc.CreateTextNode(divNode.InnerText ?? string.Empty);
                parentNode.ReplaceChild(textNode, divNode);
            }
        }
    }
    private void RemoveNodesButKeepContent(HtmlDocument htmlDoc, string[] tags)
    {
        if (htmlDoc?.DocumentNode == null || tags == null)
            return;

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag)) continue;

            var nodes = htmlDoc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes != null)
            {
                foreach (var node in nodes.ToList())
                {
                    if (node.ParentNode == null) continue;

                    var parentNode = node.ParentNode;
                    foreach (var child in node.ChildNodes.ToList())
                    {
                        if (!string.IsNullOrWhiteSpace(child.OuterHtml))
                        {
                            var newNode = HtmlNode.CreateNode(child.OuterHtml);
                            if (newNode != null)
                                parentNode.InsertBefore(newNode, node);
                        }
                    }
                    parentNode.RemoveChild(node);
                }
            }
        }
    }

    private void RemoveNodes(HtmlDocument htmlDoc, string[] tags)
    {
        if (htmlDoc?.DocumentNode == null || tags == null)
            return;

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag)) continue;

            var nodes = htmlDoc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes != null)
            {
                foreach (var node in nodes.ToList())
                {
                    node.Remove();
                }
            }
        }
    }

    private static string StripHtmlTags(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var result = html;

        var match = Regex.Match(result, @"<+(.+?)>+", RegexOptions.Singleline);
        if (match.Success)
        {
            result = match.Groups[1].Value;
        }

        result = Regex.Replace(result, @"<script[^>]*>.*?</script>", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        result = Regex.Replace(result, @"<style[^>]*>.*?</style>", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        result = Regex.Replace(result, @"<!--.*?-->", string.Empty, RegexOptions.Multiline | RegexOptions.Singleline);

        result = Regex.Replace(result, @"<[^>]*>", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);

        result = result.Replace("&nbsp;", " ")
                    .Replace("&amp;", "&")
                    .Replace("&lt;", "<")
                    .Replace("&gt;", ">")
                    .Replace("&quot;", "\"")
                    .Replace("&#39;", "'")
                    .Replace("&apos;", "'")
                    .Replace("&copy;", "©")
                    .Replace("&reg;", "®")
                    .Replace("&trade;", "™");

        result = Regex.Replace(result, @"&[a-zA-Z0-9#]+;", string.Empty);

        result = Regex.Replace(result, @"\s+", " ");

        result = result.Trim();

        return result;
    }
}
