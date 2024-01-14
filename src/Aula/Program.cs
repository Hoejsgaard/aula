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

		var minUddannelseClient =
			new MinUddannelseClient(config.AulaCredentials.Username, config.AulaCredentials.Password);
		if (await minUddannelseClient.LoginAsync())
			Console.WriteLine("Login to MinUddannelse successful.");
		else
			Console.WriteLine("Login to MinUddannelse failed.");

		Child child = config.Children[0];

		var weekLetter = await minUddannelseClient.GetWeekLetter(child, DateOnly.FromDateTime(DateTime.Today));
		await slackBot.PostWeekLetter(weekLetter, child);

		var schedule = await minUddannelseClient.GetWeekSchedule(child, DateOnly.FromDateTime(DateTime.Today));
		//Console.WriteLine("Schedule json");
		//Console.WriteLine(PrettifyJson(schedule.ToString()));


		var calendar = new GoogleCalendar(config.GoogleServiceAccount, "[skole]");
		//var hest = await calendar.GetEventsThisWeeek(config.Children[0].GoogleCalendarId);
		//await calendar.CreateEventTEST(config.Children[0].GoogleCalendarId);

		//bool success = await calendar.SynchronizeWeek(config.Children[0].GoogleCalendarId,
		//	DateOnly.FromDateTime(DateTime.Today), schedule);

		var telegram = new TelegramClient(config.Telegram.Token);
		await telegram.PostWeekLetter(config.Telegram.ChannelId, weekLetter);

		//AULA
		//var aulaClient = new AulaClient(config.AulaCredentials.Username, config.AulaCredentials.Password);
		//if (await aulaClient.LoginAsync())
		//	Console.WriteLine("Login to Aula successful.");
		//else
		//	Console.WriteLine("Login to Aula failed.");

		//var profile = await aulaClient.GetProfile();
		//var profileContext = await aulaClient.GetProfileContext();


		//Console.WriteLine("Profile: ");
		//Console.WriteLine(PrettifyJson(profile.ToString()));

		//Console.WriteLine();
		//Console.WriteLine("Profile Context: ");
		//Console.WriteLine(PrettifyJson(profileContext.ToString()));
		//Console.WriteLine();
	}
}