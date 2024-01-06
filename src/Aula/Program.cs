using Microsoft.Extensions.Configuration;

namespace Aula;

public class Program
{
	private static async Task Main()
	{
		var builder = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", true, true);

		var configuration = builder.Build();

		var username = configuration["AulaCredentials:Username"];
		if (username == null)
		{
			throw new Exception("Username Required");
		}
		var password = configuration["AulaCredentials:Password"];
		if (password == null)
		{
			throw new Exception("Password Required");
		}

		var aulaClient = new AulaClient(username, password);

		if (await aulaClient.LoginAsync())
			Console.WriteLine("Login successful.");
		else
			Console.WriteLine("Login failed.");

		var profile = await aulaClient.GetProfile();
		var profileContext = await aulaClient.GetProfileContext();


		var profileContents = await profile.Content.ReadAsStringAsync();
		Console.WriteLine();
		Console.WriteLine("Profile: ");
		Console.WriteLine(profileContents);

		var profileContextContents = await profileContext.Content.ReadAsStringAsync();
		Console.WriteLine();
		Console.WriteLine("Profile Context: ");
		Console.WriteLine(profileContextContents);

  Console.WriteLine("whatever");
	}
}