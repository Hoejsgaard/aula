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
	public async Task<bool> Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
	{
		var slackBot = new SlackBot(_config.Slack.WebhookUrl);
		return await slackBot.SendTestMessage("Hey from Rune's function");
	}
}