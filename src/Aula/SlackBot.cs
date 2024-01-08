using System.Runtime.CompilerServices;
using Html2Markdown;
using Newtonsoft.Json.Linq;
using Slack.Webhooks;

namespace Aula;

public class SlackBot
{
	private readonly SlackClient _slackClient;
	private readonly Converter _html2MarkdownConverter;

	public SlackBot(string webhookUrl)
	{
		_slackClient = new SlackClient(webhookUrl);
		_html2MarkdownConverter = new Converter();
	}

	public Task<bool> PushWeekLetter(JObject weekLetter)
	{
		var markdown = _html2MarkdownConverter.Convert(
			weekLetter["ugebreve"]?[0]?["indhold"]?.ToString().Replace("**", "*") ?? "");

		SlackMessage message = new SlackMessage()
		{
			Text = markdown,
			Markdown = true
		};

		return _slackClient.PostAsync(message);
	}
}