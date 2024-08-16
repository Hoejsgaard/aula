using System.Text.RegularExpressions;
using Html2Markdown;
using HtmlAgilityPack;

namespace Aula;

public class Html2SlackMarkdownConverter
{
	private readonly Converter _converter;

	public Html2SlackMarkdownConverter()
	{
		_converter = new Converter();
	}
	public string Convert(string? html)
	{
		if (html == null)
		{
			return "";
		}
		// behold the horrors of brittle replacements and hope we get something good out of it. 
		var markdown = _converter.Convert(html).Replace("**", "*").Replace("\u00A0", " ").Replace("*   ", "- ");


		// You'd think the above would be enough, but more often than not, the input is of so poor quality that Html2Markdown will leave artefacts.

		// Let's strip what we know can go wrong before returning the markdown. 
		var htmlDoc = new HtmlDocument();
		htmlDoc.LoadHtml(markdown);

		// Clean the document
		CleanHtmlDocument(htmlDoc);

		// Get the cleaned HTML as a string
		return htmlDoc.DocumentNode.InnerHtml;
	}
	private void CleanHtmlDocument(HtmlDocument htmlDoc)
	{
		// Remove <span> and <div> tags entirely, keeping their inner content
		RemoveNodesButKeepContent(htmlDoc, new[] { "span", "div" });

		// Remove all other unnecessary tags and inline styles
		RemoveNodes(htmlDoc, new[] { "style", "br" });

		// Remove any remaining <div> tags that were not empty
		var divNodes = htmlDoc.DocumentNode.SelectNodes("//div");
		if (divNodes != null)
		{
			foreach (var divNode in divNodes)
			{
				// Replace the div node with its inner text, effectively removing the tag but keeping the content
				var parentNode = divNode.ParentNode;
				var textNode = htmlDoc.CreateTextNode(divNode.InnerText);
				parentNode.ReplaceChild(textNode, divNode);
			}
		}
	}
	private void RemoveNodesButKeepContent(HtmlDocument htmlDoc, string[] tags)
	{
		foreach (var tag in tags)
		{
			var nodes = htmlDoc.DocumentNode.SelectNodes($"//{tag}");
			if (nodes != null)
			{
				foreach (var node in nodes)
				{
					var parentNode = node.ParentNode;
					foreach (var child in node.ChildNodes)
					{
						parentNode.InsertBefore(HtmlNode.CreateNode(child.OuterHtml), node);
					}
					parentNode.RemoveChild(node);
				}
			}
		}
	}

	private void RemoveNodes(HtmlDocument htmlDoc, string[] tags)
	{
		foreach (var tag in tags)
		{
			var nodes = htmlDoc.DocumentNode.SelectNodes($"//{tag}");
			if (nodes != null)
			{
				foreach (var node in nodes)
				{
					node.Remove();
				}
			}
		}
	}
}