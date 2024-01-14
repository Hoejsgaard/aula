using Aula;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.IO;
using Microsoft.Extensions.DependencyInjection;


var host = new HostBuilder()
	.ConfigureFunctionsWorkerDefaults()
	.ConfigureAppConfiguration((context, builder) =>
	{
		builder.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
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