using System.IO;
using System.Net.Http.Headers;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aula.Integration;

public class MinUddannelseClient : UniLoginClient, IMinUddannelseClient
{
    private JObject _userProfile = new();
    private readonly ISupabaseService? _supabaseService;
    private readonly ILogger? _logger;
    private readonly Config? _config;

    public MinUddannelseClient(Config config) : this(config.UniLogin.Username, config.UniLogin.Password)
    {
        _config = config;
    }

    public MinUddannelseClient(Config config, ISupabaseService supabaseService, ILoggerFactory loggerFactory)
        : this(config.UniLogin.Username, config.UniLogin.Password)
    {
        _config = config;
        _supabaseService = supabaseService;
        _logger = loggerFactory.CreateLogger<MinUddannelseClient>();
    }

    public MinUddannelseClient(string username, string password) : base(username, password,
        "https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False",
        "https://www.minuddannelse.net/Node/")
    {
    }

    public async Task<JObject> GetWeekLetter(Child child, DateOnly date, bool allowLiveFetch = false)
    {
        var weekNumber = GetIsoWeekNumber(date);
        var year = date.Year;

        // Step 1: Check database first
        if (_supabaseService != null)
        {
            var storedLetter = await GetStoredWeekLetter(child, weekNumber, year);
            if (storedLetter != null)
            {
                _logger?.LogInformation("📚 Found week letter in database for {ChildName} week {WeekNumber}/{Year}",
                    child.FirstName, weekNumber, year);
                return storedLetter;
            }
        }

        // Step 2: If not in database and live fetch not allowed, return empty
        if (!allowLiveFetch)
        {
            _logger?.LogInformation("🚫 Week letter not in database and live fetch not allowed for {ChildName} week {WeekNumber}/{Year}",
                child.FirstName, weekNumber, year);
            return CreateEmptyWeekLetter(weekNumber);
        }

        // Step 3: Live fetch from MinUddannelse (only if allowed)
        _logger?.LogInformation("🌐 Fetching week letter from MinUddannelse for {ChildName} week {WeekNumber}/{Year}",
            child.FirstName, weekNumber, year);

        // Normal mode - hit the real API
        var url = string.Format(
            "https://www.minuddannelse.net/api/stamdata/ugeplan/getUgeBreve?tidspunkt={0}-W{1}&elevId={2}&_={3}"
            , date.Year, GetIsoWeekNumber(date), GetChildId(child), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
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
                ["uge"] = $"{GetIsoWeekNumber(date)}",
                ["indhold"] = "Der er ikke skrevet nogen ugenoter til denne uge",
            };
            weekLetter["ugebreve"] = new JArray(nullObject);

        }

        // Store to database if we have Supabase service
        if (_supabaseService != null && weekLetter != null)
        {
            try
            {
                var contentHash = ComputeContentHash(weekLetter.ToString());
                await _supabaseService.StoreWeekLetterAsync(
                    child.FirstName, weekNumber, year, contentHash, weekLetter.ToString());
                _logger?.LogInformation("💾 Stored week letter to database for {ChildName} week {WeekNumber}/{Year}",
                    child.FirstName, weekNumber, year);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to store week letter to database");
            }
        }

        return weekLetter ?? CreateEmptyWeekLetter(weekNumber);
    }

    public async Task<JObject> GetWeekSchedule(Child child, DateOnly date)
    {
        var url = string.Format(
            "https://www.minuddannelse.net/api/stamdata/aulaskema/getElevSkema?elevId={0}&tidspunkt={1}-W{2}&_={3}",
            GetChildId(child), date.Year, GetIsoWeekNumber(date), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var response = await HttpClient.GetAsync(url);
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

    private static int GetIsoWeekNumber(DateOnly date)
    {
        return System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
    }

    public new async Task<bool> LoginAsync()
    {
        var login = await base.LoginAsync();
        if (login)
        {
            HttpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            _userProfile = await ExtractUserProfile();
            return true;
        }

        return login;
    }

    private async Task<JObject> ExtractUserProfile()
    {
        var response = await HttpClient.GetAsync(SuccessUrl);
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
        if (_supabaseService == null)
        {
            _logger?.LogWarning("Supabase service not available - cannot retrieve stored week letter");
            return null;
        }

        try
        {
            var storedContent = await _supabaseService.GetStoredWeekLetterAsync(child.FirstName, weekNumber, year);
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
        if (_supabaseService == null)
        {
            _logger?.LogWarning("Supabase service not available - cannot retrieve stored week letters");
            return new List<StoredWeekLetter>();
        }

        try
        {
            var childName = child?.FirstName;
            return await _supabaseService.GetStoredWeekLettersAsync(childName, year);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving stored week letters for child {ChildName}, year {Year}",
                child?.FirstName, year);
            return new List<StoredWeekLetter>();
        }
    }

    private static string ComputeContentHash(string content)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

    private static JObject CreateEmptyWeekLetter(int weekNumber)
    {
        return new JObject
        {
            ["errorMessage"] = null,
            ["ugebreve"] = new JArray(new JObject
            {
                ["klasseNavn"] = "Mock Class",
                ["uge"] = weekNumber.ToString(),
                ["indhold"] = "Der er ikke skrevet nogen ugenoter til denne uge (mock mode)"
            }),
            ["klasser"] = new JArray()
        };
    }
}
