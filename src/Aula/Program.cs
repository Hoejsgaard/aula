using Html2Markdown;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Slack.Webhooks;

namespace Aula;

public class Program
{
	private static async Task Main()
	{
		var builder = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", true, true);

		var configuration = builder.Build();
		var config = new Config();
		configuration.Bind(config);
		
		var slackBot = new SlackBot(config.Slack.WebhookUrl);

		//var minUddannelseClient =
		//	new MinUddannelseClient(config.AulaCredentials.Username, config.AulaCredentials.Password);
		//if (await minUddannelseClient.LoginAsync())
		//	Console.WriteLine("Login to MinUddannelse successful.");
		//else
		//	Console.WriteLine("Login to MinUddannelse failed.");

		//Child child = config.Children[0];

		//var weekLetter = await minUddannelseClient.GetWeekLetter(child, DateOnly.FromDateTime(DateTime.Today));
		//await slackBot.PushWeekLetterFancy(weekLetter, child);

		var aulaClient = new AulaClient(config.AulaCredentials.Username, config.AulaCredentials.Password);
		if (await aulaClient.LoginAsync())
			Console.WriteLine("Login to Aula successful.");
		else
			Console.WriteLine("Login to Aula failed.");

		var profile = await aulaClient.GetProfile();
		var profileContext = await aulaClient.GetProfileContext();


		Console.WriteLine("Profile: ");
		Console.WriteLine(PrettifyJson(profile.ToString()));

		Console.WriteLine();
		Console.WriteLine("Profile Context: ");
		Console.WriteLine(PrettifyJson(profileContext.ToString()));
		Console.WriteLine();
	}

	public static string PrettifyJson(string json)
	{
		return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented);
	}
}