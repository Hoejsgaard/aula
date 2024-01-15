using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Aula.Api;

public class Functions
{
	private readonly IConfig _config;
	private readonly ILogger _logger;

	public Functions(ILoggerFactory loggerFactory, IConfig config)
	{
		_config = config;
		_logger = loggerFactory.CreateLogger<Functions>();
	}

	[Function("TestSlack")]
	[FixedDelayRetry(5, "00:00:10")]
	public async Task<bool> Run([TimerTrigger("0 30 3 * * Mon")] TimerInfo myTimer)
	{
		var slackBot = new SlackBot(_config.Slack.WebhookUrl);
		return await slackBot.SendTestMessage("Hey from Rune's function running in Azure - It's now " +
		                                      "Monday at 03:30");
	}

	[Function("PublishWeekLetters")]
	[FixedDelayRetry(5, "00:10:00")]
	public async Task<bool> PublishWeekLetters([TimerTrigger("0 30 11 * * Sun")] TimerInfo myTimer)
	{
		try
		{
			var minUddannelseClient = new MinUddannelseClient(_config.UniLogin.Username, _config.UniLogin.Password);

			var slackBot = new SlackBot(_config.Slack.WebhookUrl);
			var telegram = new TelegramClient(_config.Telegram.Token);

			foreach (var child in _config.Children)
			{
				var weekLetter =
					await minUddannelseClient.GetWeekLetter(child, DateOnly.FromDateTime(DateTime.Today.AddDays(2)));
				await telegram.PostWeekLetter(_config.Telegram.ChannelId, weekLetter, child);
				await slackBot.PostWeekLetter(weekLetter, child);
			}

			return true;
		}
		catch (Exception e)
		{
			_logger.LogError(e, "Error publishing week letters");
			return false;
		}
	}
}