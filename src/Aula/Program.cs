using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Aula;

public class Program
{
	private static async Task Main()
	{
		var builder = new ConfigurationBuilder();

		var assembly = Assembly.GetExecutingAssembly();
		var resourceName = "Aula.appsettings.json";

		await using (var stream = assembly.GetManifestResourceStream(resourceName))
		{
			if (stream != null)
				using (var reader = new StreamReader(stream))
				{
					var jsonConfig = await reader.ReadToEndAsync();
					builder.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(jsonConfig)));
				}
		}
		var configuration = builder.Build();
		var config = new Config();
		configuration.Bind(config);


		var slackBot = new SlackBot(config.Slack.WebhookUrl);
		var telegramBot = new TelegramClient(config.Telegram.Token);

		var minUddannelseClient =
			new MinUddannelseClient(config.UniLogin.Username, config.UniLogin.Password);
		var loggedIn = await minUddannelseClient.LoginAsync();

		if (loggedIn)
		{
			foreach (var child in config.Children)
			{
				var weekLetter = await minUddannelseClient.GetWeekLetter(child, DateOnly.FromDateTime(DateTime.Today));
				await slackBot.PostWeekLetter(weekLetter, child);
				await telegramBot.PostWeekLetter(config.Telegram.ChannelId, weekLetter, child);
			}
		}
	}
}