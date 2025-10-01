using Microsoft.Extensions.Logging;
using Supabase;
using Aula.Configuration;

namespace Aula.Services;

public static class SupabaseClientFactory
{
    public static async Task<Client> CreateClientAsync(Config config, ILogger logger)
    {
        logger.LogInformation("Initializing Supabase connection");

        var options = new SupabaseOptions
        {
            AutoConnectRealtime = false, // We don't need realtime for this use case
            AutoRefreshToken = false     // We're using service role key
        };

        var client = new Client(config.Supabase.Url, config.Supabase.ServiceRoleKey, options);
        await client.InitializeAsync();

        logger.LogInformation("Supabase client initialized successfully");
        return client;
    }
}
