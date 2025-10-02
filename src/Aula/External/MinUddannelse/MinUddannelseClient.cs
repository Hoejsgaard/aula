using System.IO;
using Aula.Core.Models;
using Aula.External.MinUddannelse;
using Aula.External.Authentication;
using System.Net.Http.Headers;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Repositories;
using Aula.Core.Models;
using Aula.Content.WeekLetters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aula.External.MinUddannelse;

public class MinUddannelseClient : UniLoginAuthenticatorBase, IMinUddannelseClient
{
    private JObject _userProfile = new();
    private readonly IWeekLetterRepository? _weekLetterRepository;
    private readonly ILogger? _logger;
    private readonly Config? _config;

    public MinUddannelseClient(Config config, IHttpClientFactory httpClientFactory)
        : this(httpClientFactory, config.UniLogin.Username, config.UniLogin.Password,
            config.MinUddannelse.SamlLoginUrl,
            config.MinUddannelse.ApiBaseUrl + "/Node/",
            config.MinUddannelse.ApiBaseUrl,
            config.MinUddannelse.StudentDataPath,
            null)
    {
        _config = config;
    }

    public MinUddannelseClient(Config config, IWeekLetterRepository weekLetterRepository, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
        : this(httpClientFactory, config.UniLogin.Username, config.UniLogin.Password,
            config.MinUddannelse.SamlLoginUrl,
            config.MinUddannelse.ApiBaseUrl + "/Node/",
            config.MinUddannelse.ApiBaseUrl,
            config.MinUddannelse.StudentDataPath,
            loggerFactory.CreateLogger<MinUddannelseClient>())
    {
        _config = config;
        _weekLetterRepository = weekLetterRepository;
    }

    public MinUddannelseClient(IHttpClientFactory httpClientFactory, string username, string password, string loginUrl, string successUrl, string apiBaseUrl, string studentDataPath, ILogger? logger = null)
        : base(httpClientFactory, username, password, loginUrl, successUrl,
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            apiBaseUrl,
            studentDataPath)
    {
        _logger = logger;
    }

    public async Task<JObject> GetWeekLetter(Child child, DateOnly date, bool allowLiveFetch = false)
    {
        var weekNumber = WeekLetterUtilities.GetIsoWeekNumber(date);
        var year = date.Year;

        if (_weekLetterRepository != null)
        {
            var storedLetter = await GetStoredWeekLetter(child, weekNumber, year);
            if (storedLetter != null)
            {
                _logger?.LogInformation("Found week letter in database for {ChildName} week {WeekNumber}/{Year}",
                    child.FirstName, weekNumber, year);
                return storedLetter;
            }
        }

        if (!allowLiveFetch)
        {
            _logger?.LogInformation("Week letter not in database and live fetch not allowed for {ChildName} week {WeekNumber}/{Year}",
                child.FirstName, weekNumber, year);
            return WeekLetterUtilities.CreateEmptyWeekLetter(weekNumber);
        }

        _logger?.LogInformation("Fetching week letter from MinUddannelse for {ChildName} week {WeekNumber}/{Year}",
            child.FirstName, weekNumber, year);

        var url = string.Format(
            "{0}{1}?tidspunkt={2}-W{3}&elevId={4}&_={5}",
            _config?.MinUddannelse.ApiBaseUrl ?? "https://www.minuddannelse.net",
            _config?.MinUddannelse.WeekLettersPath ?? "/api/stamdata/ugeplan/getUgeBreve",
            date.Year, WeekLetterUtilities.GetIsoWeekNumber(date), GetChildId(child), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync(url);
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

        if (_weekLetterRepository != null && weekLetter != null)
        {
            try
            {
                var contentHash = WeekLetterUtilities.ComputeContentHash(weekLetter.ToString());
                await _weekLetterRepository.StoreWeekLetterAsync(
                    child.FirstName, weekNumber, year, contentHash, weekLetter.ToString());
                _logger?.LogInformation("Stored week letter to database for {ChildName} week {WeekNumber}/{Year}",
                    child.FirstName, weekNumber, year);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to store week letter to database");
            }
        }

        return weekLetter ?? WeekLetterUtilities.CreateEmptyWeekLetter(weekNumber);
    }

    public async Task<JObject> GetWeekSchedule(Child child, DateOnly date)
    {
        var url = string.Format(
            "{0}/api/stamdata/aulaskema/getElevSkema?elevId={1}&tidspunkt={2}-W{3}&_={4}",
            _config?.MinUddannelse.ApiBaseUrl ?? "https://www.minuddannelse.net",
            GetChildId(child), date.Year, WeekLetterUtilities.GetIsoWeekNumber(date), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JObject.Parse(json);
    }


    private string? GetChildId(Child child)
    {
        if (_userProfile == null) throw new InvalidOperationException("User profile not loaded");
        var kids = _userProfile["boern"];
        if (kids == null) throw new InvalidOperationException("No children found in user profile");
        var id = "";
        foreach (var kid in kids)
            if (kid["fornavn"]?.ToString() == child.FirstName)
                id = kid["id"]?.ToString() ?? "";

        if (id == "") throw new ArgumentException($"Child with first name '{child.FirstName}' not found in user profile");

        return id;
    }

    public new async Task<bool> LoginAsync()
    {
        var login = await base.LoginAsync();
        if (login)
        {
            _userProfile = await ExtractUserProfile();
            return true;
        }

        return login;
    }

    private async Task<JObject> ExtractUserProfile()
    {
        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync(SuccessUrl);
        var content = await response.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        // Find the script node that contains __tempcontext__5d
        var script = doc.DocumentNode.Descendants("script")
            .FirstOrDefault(n => n.InnerText.Contains("__tempcontext__"));

        if (script == null)
            throw new InvalidDataException("No UserProfile script tag found in response");

        var scriptText = script.InnerText;
        if (string.IsNullOrWhiteSpace(scriptText))
            throw new InvalidDataException("UserProfile script content is empty");

        var contextStart = "window.__tempcontext__['currentUser'] = ";
        var startIndex = scriptText.IndexOf(contextStart);
        if (startIndex == -1)
            throw new InvalidDataException("UserProfile context not found in script");

        startIndex += contextStart.Length;
        var endIndex = scriptText.IndexOf(';', startIndex);
        if (endIndex == -1 || endIndex <= startIndex)
            throw new InvalidDataException("Invalid UserProfile context format");

        var jsonText = scriptText.Substring(startIndex, endIndex - startIndex).Trim();
        if (string.IsNullOrWhiteSpace(jsonText))
            throw new InvalidDataException("Extracted UserProfile JSON text is empty");

        try
        {
            return JObject.Parse(jsonText);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Failed to parse UserProfile JSON: {ex.Message}", ex);
        }
    }

    public async Task<JObject?> GetStoredWeekLetter(Child child, int weekNumber, int year)
    {
        if (_weekLetterRepository == null)
        {
            _logger?.LogWarning("Week letter repository not available - cannot retrieve stored week letter");
            return null;
        }

        try
        {
            var storedContent = await _weekLetterRepository.GetStoredWeekLetterAsync(child.FirstName, weekNumber, year);
            if (string.IsNullOrEmpty(storedContent))
            {
                _logger?.LogInformation("No stored week letter found for {ChildName}, week {WeekNumber}/{Year}",
                    child.FirstName, weekNumber, year);
                return null;
            }

            return JObject.Parse(storedContent);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving stored week letter for {ChildName}, week {WeekNumber}/{Year}",
                child.FirstName, weekNumber, year);
            return null;
        }
    }


    public async Task<List<StoredWeekLetter>> GetStoredWeekLetters(Child? child = null, int? year = null)
    {
        if (_weekLetterRepository == null)
        {
            _logger?.LogWarning("Week letter repository not available - cannot retrieve stored week letters");
            return new List<StoredWeekLetter>();
        }

        try
        {
            var childName = child?.FirstName;
            return await _weekLetterRepository.GetStoredWeekLettersAsync(childName, year);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving stored week letters for child {ChildName}, year {Year}",
                child?.FirstName, year);
            return new List<StoredWeekLetter>();
        }
    }
}
