using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using MinUddannelse.Configuration;
using MinUddannelse.Content.WeekLetters;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;

namespace MinUddannelse.Client;

/// <summary>
/// MinUddannelse client that orchestrates per-child authentication for live fetching
/// Pure orchestration - no database access
/// </summary>
public class MinUddannelseClient : IMinUddannelseClient, IDisposable
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly Dictionary<string, (IChildAuthenticatedClient Client, DateTime ExpiresAt)> _clientCache = new();
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(25);

    public MinUddannelseClient(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MinUddannelseClient>();
        _httpClientFactory = httpClientFactory;
    }

    private async Task<IChildAuthenticatedClient> GetOrCreateClientAsync(Child child)
    {
        var cacheKey = $"{child.FirstName}_{child.LastName}";
        var now = DateTime.UtcNow;

        if (_clientCache.TryGetValue(cacheKey, out var cached))
        {
            if (cached.ExpiresAt > now)
            {
                _logger.LogDebug("Using cached authenticated client for {ChildName}", child.FirstName);
                return cached.Client;
            }
            else
            {
                _logger.LogDebug("Cached client expired for {ChildName}, disposing and recreating", child.FirstName);
                cached.Client.Dispose();
                _clientCache.Remove(cacheKey);
            }
        }

        if (child.UniLogin == null || string.IsNullOrEmpty(child.UniLogin.Username) ||
            (string.IsNullOrEmpty(child.UniLogin.Password) && (child.UniLogin.PictogramSequence == null || child.UniLogin.PictogramSequence.Length == 0)))
        {
            throw new InvalidOperationException($"No credentials available for {child.FirstName}");
        }

        var logger = _loggerFactory.CreateLogger<MinUddannelseClient>();
        IChildAuthenticatedClient client = child.UniLogin.AuthType == AuthenticationType.Pictogram && child.UniLogin.PictogramSequence != null
            ? new PictogramAuthenticatedClient(child, child.UniLogin.Username, child.UniLogin.PictogramSequence, logger, _httpClientFactory)
            : new ChildAuthenticatedClient(child, child.UniLogin.Username, child.UniLogin.Password, logger, _httpClientFactory);

        _logger.LogInformation("Using {AuthType} authentication for {ChildName}",
            child.UniLogin.AuthType == AuthenticationType.Pictogram ? "pictogram" : "standard", child.FirstName);

        var loginSuccess = await client.LoginAsync();
        if (!loginSuccess)
        {
            client.Dispose();
            throw new InvalidOperationException($"Failed to authenticate {child.FirstName}");
        }

        _logger.LogInformation("Successfully authenticated {ChildName}", child.FirstName);

        var expiresAt = now.Add(_sessionTimeout);
        _clientCache[cacheKey] = (client, expiresAt);

        return client;
    }

    public Task<bool> LoginAsync()
    {
        _logger.LogInformation("LoginAsync called - authentication will happen per-request");
        return Task.FromResult(true);
    }

    public async Task<JObject> GetWeekLetter(Child child, DateOnly date, bool allowLiveFetch = false)
    {
        var weekNumber = WeekLetterUtilities.GetIsoWeekNumber(date);

        if (!allowLiveFetch)
        {
            _logger.LogInformation("Live fetch not allowed for {ChildName} week {WeekNumber}",
                child.FirstName, weekNumber);
            return WeekLetterUtilities.CreateEmptyWeekLetter(weekNumber);
        }

        _logger.LogInformation("Live fetching week letter from MinUddannelse for {ChildName} week {WeekNumber}",
            child.FirstName, weekNumber);

        try
        {
            var client = await GetOrCreateClientAsync(child);
            var weekLetter = await client.GetWeekLetter(date);
            return weekLetter ?? WeekLetterUtilities.CreateEmptyWeekLetter(weekNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get week letter for {ChildName}", child.FirstName);
            return WeekLetterUtilities.CreateEmptyWeekLetter(weekNumber);
        }
    }

    public async Task<JObject> GetWeekSchedule(Child child, DateOnly date)
    {
        _logger.LogInformation("Live fetching week schedule for {ChildName}", child.FirstName);

        try
        {
            var client = await GetOrCreateClientAsync(child);
            return await client.GetWeekSchedule(date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get week schedule for {ChildName}", child.FirstName);
            return new JObject();
        }
    }

    public void Dispose()
    {
        foreach (var (client, _) in _clientCache.Values)
        {
            client?.Dispose();
        }
        _clientCache.Clear();
    }
}
