using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Repositories;
using Aula.Services;
using Aula.Utilities;

namespace Aula.Integration;

/// <summary>
/// MinUddannelse client that authenticates per-child instead of using a parent account
/// Each child has their own UniLogin credentials
/// </summary>
public partial class PerChildMinUddannelseClient : IMinUddannelseClient
{
    private readonly IWeekLetterRepository? _weekLetterRepository;
    private readonly IRetryTrackingRepository? _retryTrackingRepository;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Config _config;

    // No longer storing authenticated clients - creating fresh instances per request

    public PerChildMinUddannelseClient(Config config, IWeekLetterRepository? weekLetterRepository, IRetryTrackingRepository? retryTrackingRepository, ILoggerFactory loggerFactory)
    {
        _config = config;
        _weekLetterRepository = weekLetterRepository;
        _retryTrackingRepository = retryTrackingRepository;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PerChildMinUddannelseClient>();
    }

    public Task<bool> LoginAsync()
    {
        // This method is now a no-op since we authenticate per-request
        // Keeping it for backward compatibility with IMinUddannelseClient interface
        _logger.LogInformation("LoginAsync called - authentication will happen per-request");
        return Task.FromResult(true);
    }

    public async Task<JObject> GetWeekLetter(Child child, DateOnly date, bool allowLiveFetch = false)
    {
        var weekNumber = WeekLetterUtilities.GetIsoWeekNumber(date);
        var year = date.Year;

        // Step 1: Check database first
        if (_weekLetterRepository != null)
        {
            var storedLetter = await GetStoredWeekLetter(child, weekNumber, year);
            if (storedLetter != null)
            {
                _logger.LogInformation("📚 Found week letter in database for {ChildName} week {WeekNumber}/{Year}",
                    child.FirstName, weekNumber, year);
                return storedLetter;
            }
        }

        // Step 2: If not in database and live fetch not allowed, return empty
        if (!allowLiveFetch)
        {
            _logger.LogInformation("🚫 Week letter not in database and live fetch not allowed for {ChildName} week {WeekNumber}/{Year}",
                child.FirstName, weekNumber, year);
            return WeekLetterUtilities.CreateEmptyWeekLetter(weekNumber);
        }

        // Step 3: Live fetch from MinUddannelse (only if allowed)
        _logger.LogInformation("🌐 Fetching week letter from MinUddannelse for {ChildName} week {WeekNumber}/{Year}",
            child.FirstName, weekNumber, year);

        // Check credentials - need username and either password or pictogram sequence
        if (child.UniLogin == null || string.IsNullOrEmpty(child.UniLogin.Username) ||
            (string.IsNullOrEmpty(child.UniLogin.Password) && (child.UniLogin.PictogramSequence == null || child.UniLogin.PictogramSequence.Length == 0)))
        {
            _logger.LogError("❌ No credentials available for {ChildName}", child.FirstName);
            return WeekLetterUtilities.CreateEmptyWeekLetter(weekNumber);
        }

        // Create fresh authenticated client and fetch
        _logger.LogInformation("🔑 Creating fresh authenticated session for {ChildName}", child.FirstName);

        // Choose authentication method based on config
        IChildAuthenticatedClient childClient;
        var baseLogger = _loggerFactory.CreateLogger<UniLoginAuthenticatorBase>();
        if (child.UniLogin.AuthType == AuthenticationType.Pictogram && child.UniLogin.PictogramSequence != null)
        {
            _logger.LogInformation("Using pictogram authentication for {ChildName}", child.FirstName);
            childClient = new PictogramAuthenticatedClient(child, child.UniLogin.Username, child.UniLogin.PictogramSequence, _config, baseLogger, _logger);
        }
        else
        {
            _logger.LogInformation("Using standard authentication for {ChildName}", child.FirstName);
            childClient = new ChildAuthenticatedClient(child, child.UniLogin.Username, child.UniLogin.Password, _config, _logger);
        }

        var loginSuccess = await childClient.LoginAsync();
        if (!loginSuccess)
        {
            _logger.LogError("Failed to authenticate {ChildName}", child.FirstName);
            return WeekLetterUtilities.CreateEmptyWeekLetter(weekNumber);
        }

        _logger.LogInformation("Successfully authenticated {ChildName} for this request", child.FirstName);

        // Fetch from MinUddannelse
        var weekLetter = await childClient.GetWeekLetter(date);

        // Store to database if we have repository
        if (_weekLetterRepository != null && weekLetter != null)
        {
            try
            {
                var contentHash = WeekLetterUtilities.ComputeContentHash(weekLetter.ToString());
                await _weekLetterRepository.StoreWeekLetterAsync(
                    child.FirstName, weekNumber, year, contentHash, weekLetter.ToString());
                _logger.LogInformation("💾 Stored week letter to database for {ChildName} week {WeekNumber}/{Year}",
                    child.FirstName, weekNumber, year);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store week letter to database");
            }
        }

        return weekLetter ?? WeekLetterUtilities.CreateEmptyWeekLetter(weekNumber);
    }

    public async Task<JObject> GetWeekSchedule(Child child, DateOnly date)
    {
        // Create a fresh authenticated client for this request
        // Check credentials - need username and either password or pictogram sequence
        if (child.UniLogin == null || string.IsNullOrEmpty(child.UniLogin.Username) ||
            (string.IsNullOrEmpty(child.UniLogin.Password) && (child.UniLogin.PictogramSequence == null || child.UniLogin.PictogramSequence.Length == 0)))
        {
            _logger.LogError("❌ No credentials available for {ChildName}", child.FirstName);
            return new JObject();
        }

        _logger.LogInformation("Creating fresh authenticated session for {ChildName} (schedule request)", child.FirstName);

        // Choose authentication method based on config
        IChildAuthenticatedClient childClient;
        var baseLogger = _loggerFactory.CreateLogger<UniLoginAuthenticatorBase>();
        if (child.UniLogin.AuthType == AuthenticationType.Pictogram && child.UniLogin.PictogramSequence != null)
        {
            _logger.LogInformation("Using pictogram authentication for {ChildName}", child.FirstName);
            childClient = new PictogramAuthenticatedClient(child, child.UniLogin.Username, child.UniLogin.PictogramSequence, _config, baseLogger, _logger);
        }
        else
        {
            _logger.LogInformation("Using standard authentication for {ChildName}", child.FirstName);
            childClient = new ChildAuthenticatedClient(child, child.UniLogin.Username, child.UniLogin.Password, _config, _logger);
        }

        var loginSuccess = await childClient.LoginAsync();
        if (!loginSuccess)
        {
            _logger.LogError("Failed to authenticate {ChildName} for schedule request", child.FirstName);
            return new JObject();
        }

        _logger.LogInformation("Successfully authenticated {ChildName} for schedule request", child.FirstName);

        // Use the fresh client to get their week schedule
        return await childClient.GetWeekSchedule(date);
    }


    public async Task<JObject?> GetStoredWeekLetter(Child child, int weekNumber, int year)
    {
        if (_weekLetterRepository == null)
        {
            _logger.LogWarning("Supabase service not available");
            return null;
        }

        try
        {
            var storedContent = await _weekLetterRepository.GetStoredWeekLetterAsync(
                child.FirstName, weekNumber, year);

            if (string.IsNullOrEmpty(storedContent))
            {
                return null;
            }

            return JObject.Parse(storedContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored week letter");
            return null;
        }
    }

    public async Task<List<StoredWeekLetter>> GetStoredWeekLetters(Child? child = null, int? year = null)
    {
        if (_weekLetterRepository == null)
        {
            return new List<StoredWeekLetter>();
        }

        try
        {
            return await _weekLetterRepository.GetStoredWeekLettersAsync(child?.FirstName, year);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored week letters");
            return new List<StoredWeekLetter>();
        }
    }

    /// <summary>
    /// Inner class that handles authentication for a specific child
    /// </summary>
    private sealed partial class ChildAuthenticatedClient : UniLoginAuthenticatorBase, IChildAuthenticatedClient
    {
        private readonly Child _child;
        private readonly ILogger _childLogger;
        private readonly Config _config;
        private string? _childId;

        public ChildAuthenticatedClient(Child child, string username, string password, Config config, ILogger logger)
            : base(username, password,
                config.MinUddannelse.SamlLoginUrl,
                config.MinUddannelse.ApiBaseUrl,
                (ILogger<UniLoginAuthenticatorBase>)logger,
                config.MinUddannelse.ApiBaseUrl,
                config.MinUddannelse.StudentDataPath)
        {
            _child = child;
            _childLogger = logger;
            _config = config;
        }

        public new async Task<bool> LoginAsync()
        {
            _childLogger.LogInformation("Attempting to login for {ChildName} at URL: {Url}",
                _child.FirstName, _config.MinUddannelse.ApiBaseUrl);

            var loginSuccess = await base.LoginAsync();

            _childLogger.LogInformation("Base login returned: {Success}", loginSuccess);

            if (loginSuccess)
            {
                HttpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                _childLogger.LogInformation("Attempting to extract child ID for {ChildName}", _child.FirstName);

                // When logged in as the child, we need to extract their own ID
                _childId = await ExtractChildId();

                if (string.IsNullOrEmpty(_childId))
                {
                    _childLogger.LogError("Could not extract child ID for {ChildName}", _child.FirstName);
                    return false;
                }

                _childLogger.LogInformation("Extracted child ID for {ChildName}: {ChildId}", _child.FirstName, _childId);
                return true;
            }

            _childLogger.LogError("Base login failed for {ChildName}", _child.FirstName);
            return false;
        }

        private async Task<string?> ExtractChildId()
        {
            try
            {
                _childLogger.LogInformation("Extracting child ID for {ChildName}", _child.FirstName);

                // The authenticated page contains the child ID in the __tempcontext__ object
                // We need to navigate to any MinUddannelse page after authentication to get this
                var response = await HttpClient.GetAsync($"{_config.MinUddannelse.ApiBaseUrl}/node/minuge");
                var content = await response.Content.ReadAsStringAsync();

                // Look for personid in the __tempcontext__ object
                // Format: "personid":2643430
                var personIdMatch = PersonIdRegex().Match(content);
                if (personIdMatch.Success)
                {
                    var childId = personIdMatch.Groups[1].Value;
                    _childLogger.LogInformation("Extracted child ID from page context: {ChildId}", childId);

                    // Verify the user name matches what we expect
                    var nameMatch = NameRegex().Match(content);
                    if (nameMatch.Success)
                    {
                        var firstName = nameMatch.Groups[1].Value;
                        var lastName = nameMatch.Groups[2].Value;
                        _childLogger.LogInformation("Confirmed authenticated as: {FirstName} {LastName}",
                            firstName, lastName);
                    }

                    return childId;
                }

                // Fallback: Try the old API method
                _childLogger.LogInformation("Page context method failed, trying API");
                var apiUrl = $"{_config.MinUddannelse.ApiBaseUrl}{_config.MinUddannelse.StudentDataPath}?_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

                var apiResponse = await HttpClient.GetAsync(apiUrl);
                if (apiResponse.IsSuccessStatusCode)
                {
                    var apiContent = await apiResponse.Content.ReadAsStringAsync();
                    if (apiContent.StartsWith('{') || apiContent.StartsWith('['))
                    {
                        var studentData = JObject.Parse(apiContent);
                        var childId = studentData["id"]?.ToString() ??
                                     studentData["elevId"]?.ToString() ??
                                     studentData["personid"]?.ToString();

                        if (!string.IsNullOrEmpty(childId))
                        {
                            _childLogger.LogInformation("Extracted child ID from API: {ChildId}", childId);
                            return childId;
                        }
                    }
                }

                _childLogger.LogWarning("Could not extract child ID from any source");
                return null;
            }
            catch (Exception ex)
            {
                _childLogger.LogError(ex, "Error extracting child ID");
                return null;
            }
        }

        public async Task<JObject> GetWeekLetter(DateOnly date)
        {
            if (string.IsNullOrEmpty(_childId))
            {
                _childLogger.LogError("Child ID not available for {ChildName}", _child.FirstName);
                return new JObject();
            }

            var url = $"{_config.MinUddannelse.ApiBaseUrl}{_config.MinUddannelse.WeekLettersPath}?tidspunkt={date.Year}-W{WeekLetterUtilities.GetIsoWeekNumber(date)}" +
                     $"&elevId={_childId}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            var weekLetter = JObject.Parse(json);
            var weekLetterArray = weekLetter["ugebreve"] as JArray;

            if (weekLetterArray == null || weekLetterArray.Count == 0)
            {
                var nullObject = new JObject
                {
                    ["klasseNavn"] = "N/A",
                    ["uge"] = $"{WeekLetterUtilities.GetIsoWeekNumber(date)}",
                    ["indhold"] = "Der er ikke skrevet nogen ugenoter til denne uge",
                };
                weekLetter["ugebreve"] = new JArray(nullObject);
            }

            return weekLetter;
        }

        public async Task<JObject> GetWeekSchedule(DateOnly date)
        {
            if (string.IsNullOrEmpty(_childId))
            {
                _childLogger.LogError("Child ID not available for {ChildName}", _child.FirstName);
                return new JObject();
            }

            var url = $"{_config.MinUddannelse.ApiBaseUrl}/api/stamdata/aulaskema/getElevSkema?elevId={_childId}" +
                     $"&tidspunkt={date.Year}-W{WeekLetterUtilities.GetIsoWeekNumber(date)}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JObject.Parse(json);
        }

        [GeneratedRegex(@"""personid"":(\d+)")]
        private static partial Regex PersonIdRegex();

        [GeneratedRegex(@"""fornavn"":""([^""]*)"",""efternavn"":""([^""]*)""")]
        private static partial Regex NameRegex();
    }
}
