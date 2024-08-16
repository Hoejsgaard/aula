using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace Aula;

public class Program
{
	private static async Task Main()
	{
		try
		{
			Console.WriteLine("Starting aula");
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

			Console.WriteLine("App loaded");


			var slackBot = new SlackBot(config.Slack.WebhookUrl);
			Console.WriteLine("Slackbot configured with " + config.Slack.WebhookUrl);
			var telegramBot = new TelegramClient(config.Telegram.Token);
			Console.WriteLine("App loaded");


			var minUddannelseClient =
				new MinUddannelseClient(config.UniLogin.Username, config.UniLogin.Password);
			var loggedIn = await minUddannelseClient.LoginAsync();

			if (loggedIn)
			{
				Console.WriteLine("Successfully logged into MinUddannelse");
				foreach (var child in config.Children)
				{
					Console.WriteLine("Fetching week letter for " + child.FirstName);
					var weekLetter =
						await minUddannelseClient.GetWeekLetter(child,
							DateOnly.FromDateTime(DateTime.Today.AddDays(1))); // expected to run on sundays, shrug
					// make null object 'no week letter'
					await slackBot.PostWeekLetter(weekLetter, child);
					await telegramBot.PostWeekLetter(config.Telegram.ChannelId, weekLetter, child);
				}
			}
			else
			{
				Console.WriteLine("Failed to log into MinUddannelse");
			}
		}
		catch (Exception e)
		{
			Console.WriteLine("An error occurred : ");
			Console.WriteLine(e);
			throw;
		}
	}
}