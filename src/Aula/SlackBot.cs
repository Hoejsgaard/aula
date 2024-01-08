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

	public Task<bool> PushWeekLetterFancy(JObject weekLetter)
	{
		var markdown = _html2MarkdownConverter.Convert(weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "").Replace("**", "*");
		
		SlackMessage message = new SlackMessage()
		{
			Attachments = new List<SlackAttachment>()
			{
				new SlackAttachment()
			    {
				  MarkdownIn = new List<string>() {"text"},
				  Color = "36a64f", // Kid's color
				  Pretext = "Baggegårdsskolen 3.C ugebrev", // To be fixed kid and week
				  AuthorName = "MinUddannelse",
				  AuthorLink = "https://www.minuddannelse.net/Node/minuge/fromwhereid?",
				  Text = markdown
				}
			},
			Markdown = true
		};

		return _slackClient.PostAsync(message);
	}
}