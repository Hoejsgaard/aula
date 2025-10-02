using System.Text.RegularExpressions;
using Aula.Content.Processing;
using Html2Markdown;
using HtmlAgilityPack;

namespace Aula.Content.Processing;

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
            // behold the horrors of brittle replacements and hope we get something good out of it. 
            var markdown = _converter.Convert(html).Replace("**", "*").Replace("\u00A0", " ").Replace("*   ", "- ");


            // You'd think the above would be enough, but more often than not, the input is of so poor quality that Html2Markdown will leave artefacts.

            // Let's strip what we know can go wrong before returning the markdown. 
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(markdown);

            // Clean the document
            CleanHtmlDocument(htmlDoc);

            // Get the cleaned HTML as a string
            return htmlDoc.DocumentNode?.InnerHtml ?? string.Empty;
        }
        catch (Exception)
        {
            // If HTML parsing fails, return the original input stripped of HTML tags
            return Regex.Replace(html, "<.*?>", string.Empty);
        }
    }
    private void CleanHtmlDocument(HtmlDocument htmlDoc)
    {
        if (htmlDoc?.DocumentNode == null)
            return;

        // Remove <span> and <div> tags entirely, keeping their inner content
        RemoveNodesButKeepContent(htmlDoc, new[] { "span", "div" });

        // Remove all other unnecessary tags and inline styles
        RemoveNodes(htmlDoc, new[] { "style", "br" });

        // Remove any remaining <div> tags that were not empty
        var divNodes = htmlDoc.DocumentNode.SelectNodes("//div");
        if (divNodes != null)
        {
            foreach (var divNode in divNodes.ToList())
            {
                if (divNode.ParentNode == null) continue;

                // Replace the div node with its inner text, effectively removing the tag but keeping the content
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
}
