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

		var username = configuration["AulaCredentials:Username"] ?? "";
		if (username == "") throw new Exception("Username Required");
		var password = configuration["AulaCredentials:Password"] ?? "";
		if (password == "") throw new Exception("Password Required");
		var slackWebHookUrl = configuration["Slack:WebhookUrl"] ?? "";
		if (slackWebHookUrl == "") throw new Exception("Slack Web hook URL Required");

		var slackBot = new SlackBot(slackWebHookUrl);

		var minUddannelseClient = new MinUddannelseClient(username, password);
		if (await minUddannelseClient.LoginAsync())
			Console.WriteLine("Login to MinUddannelse successful.");
		else
			Console.WriteLine("Login to MinUddannelse failed.");

		var weekLetter = await minUddannelseClient.GetWeekLetter();
		await slackBot.PushWeekLetterFancy(weekLetter);

		//var converter = new Converter();
		//var content = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
		//var markdown = converter.Convert(content).Replace("**", "*");
		//var message = new SlackMessage
		//{
		//	Text = markdown,
		//	Markdown = true
		//};

		//await slackClient.PostAsync(message);

		//var test = await minUddannelseClient.TestMinUddannelseApi();
		//Console.WriteLine("Test min uddanelse: ");
		//Console.WriteLine(PrettifyJson(test));

		//var aulaClient = new AulaClient(username, password);
		//if (await aulaClient.LoginAsync())
		//	Console.WriteLine("Login to Aula successful.");
		//else
		//	Console.WriteLine("Login to Aula failed.");

		//var profile = await aulaClient.GetProfile();
		//var profileContext = await aulaClient.GetProfileContext();


		//Console.WriteLine("Profile: ");
		//Console.WriteLine(PrettifyJson(profile));

		//Console.WriteLine();
		//Console.WriteLine("Profile Context: ");
		//Console.WriteLine(PrettifyJson(profileContext));
		//Console.WriteLine();
	}

	private static string PrettifyJson(string json)
	{
		return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented);
	}
}