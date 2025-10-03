using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using Microsoft.Extensions.Logging;
using Supabase;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MinUddannelse.Repositories;

public class AppStateRepository : IAppStateRepository
{
    private readonly Supabase.Client _supabase;
    private readonly ILogger _logger;

    public AppStateRepository(Supabase.Client supabase, ILoggerFactory loggerFactory)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        _logger = loggerFactory.CreateLogger<AppStateRepository>();
    }

    public async Task<string?> GetAppStateAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await _supabase
            .From<AppState>()
            .Select("value")
            .Where(a => a.Key == key)
            .Get();

        var appState = result.Models.FirstOrDefault();
        return appState?.Value;
    }

    public async Task SetAppStateAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        // First check if the key already exists
        var existing = await _supabase
            .From<AppState>()
            .Select("*")
            .Where(a => a.Key == key)
            .Get();

        if (existing.Models.Count > 0)
        {
            // Update existing value
            await _supabase
                .From<AppState>()
                .Where(a => a.Key == key)
                .Set(a => a.Value, value)
                .Update();

            _logger.LogInformation("Updated app state: {Key} = {Value}", key, value);
        }
        else
        {
            // Insert new key-value pair
            var appState = new AppState
            {
                Key = key,
                Value = value
            };

            await _supabase
                .From<AppState>()
                .Insert(appState);

            _logger.LogInformation("Created new app state: {Key} = {Value}", key, value);
        }
    }
}
