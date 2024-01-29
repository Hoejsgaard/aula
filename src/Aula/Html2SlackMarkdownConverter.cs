using System.Text.RegularExpressions;
using Html2Markdown;

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

		html = Regex.Replace(html, "<span[^>]*>", "");
		html = html.Replace("</span>", "");

		return _converter.Convert(html).Replace("**", "*");
	}
}