using Aula;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
	.ConfigureFunctionsWorkerDefaults()
	.ConfigureAppConfiguration((context, builder) =>
	{
		builder.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", true, true)
			.AddEnvironmentVariables();
	})
	.ConfigureServices((context, services) =>
	{
		// Bind configuration to Config object
		var config = new Config();
		context.Configuration.Bind(config);
		services.AddSingleton<IConfig>(config);

		// If you have other services to configure, do it here
		// Example: services.AddSingleton<ISlackService, SlackService>();
	})
	.Build();

host.Run();