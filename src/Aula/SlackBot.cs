using System.Runtime.CompilerServices;
using Html2Markdown;
using Newtonsoft.Json.Linq;
using Slack.Webhooks;

namespace Aula;

public class SlackBot
{
	private readonly SlackClient _slackClient;
	private readonly Html2SlackMarkdownConverter _html2MarkdownConverter;

	public SlackBot(string webhookUrl)
	{
		_slackClient = new SlackClient(webhookUrl);
		_html2MarkdownConverter = new Html2SlackMarkdownConverter();
	}

	public Task<bool> PushWeekLetter(JObject weekLetter)
	{
		var markdown = _html2MarkdownConverter.Convert(weekLetter["ugebreve"]?[0]?["indhold"]?.ToString().Replace("**", "*") ?? "");

		SlackMessage message = new SlackMessage()
		{
			Text = markdown,
			Markdown = true
		};

		return _slackClient.PostAsync(message);
	}

	public Task<bool> PostWeekLetter(JObject weekLetter, Child child)
	{
		Console.WriteLine(JsonFormatter.Prettify(weekLetter.ToString()));

		var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
		var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? "";
		var letterText = _html2MarkdownConverter.Convert(weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "").Replace("**", "*");
		
		SlackMessage message = new SlackMessage()
		{
			Attachments = new List<SlackAttachment>()
			{
				new()
			    {
				  MarkdownIn = new List<string>() {"text"},
				  Color = child.Colour,
				  Pretext = $"Ugebrev for {@class} uge {week}",
				  AuthorName = "MinUddannelse",
				  AuthorLink = "https://www.minuddannelse.net/Node/minuge/fromwhereid?",
				  Text = letterText
				}
			},
			Markdown = true
		};

		return _slackClient.PostAsync(message);
	}

	public Task<bool> SendTestMessage(string message)
	{
		return _slackClient.PostAsync(new SlackMessage()
		{ 
			Text = message
		});
	}
}