using System.Globalization;
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

    public async Task<JObject> GetWeekLetter(Child child, DateOnly date)
    {
        // Check if we're in mock mode
        if (_config?.Features.UseMockData == true && _supabaseService != null)
        {
            _logger?.LogInformation("🎭 Mock mode enabled - returning stored week letter for {ChildName}", child.FirstName);
            
            // Use configured mock week/year instead of the requested date
            var mockWeek = _config.Features.MockCurrentWeek;
            var mockYear = _config.Features.MockCurrentYear;
            
            _logger?.LogInformation("🎭 Simulating week {MockWeek}/{MockYear} as current week for {ChildName}", 
                mockWeek, mockYear, child.FirstName);
            
            // Try to get stored week letter for the mock week
            var storedContent = await _supabaseService.GetStoredWeekLetterAsync(child.FirstName, mockWeek, mockYear);
            if (!string.IsNullOrEmpty(storedContent))
            {
                _logger?.LogInformation("✅ Found stored week letter for {ChildName} week {MockWeek}/{MockYear}", 
                    child.FirstName, mockWeek, mockYear);
                return JObject.Parse(storedContent);
            }
            
            _logger?.LogWarning("⚠️ No stored week letter found for {ChildName} week {MockWeek}/{MockYear} - returning empty", 
                child.FirstName, mockWeek, mockYear);
            
            // Return empty week letter if no stored data found
            return CreateEmptyWeekLetter(mockWeek);
        }
        
        // Normal mode - hit the real API
        var url = string.Format(
            "https://www.minuddannelse.net/api/stamdata/ugeplan/getUgeBreve?tidspunkt={0}-W{1}&elevId={2}&_={3}"
            , date.Year, GetIsoWeekNumber(date), GetChildId(child), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        var weekLetter = JObject.Parse(json);
        var weekLetterArray = weekLetter["ugebreve"] as JArray;

        if (weekLetterArray == null || !weekLetterArray.Any())
        {
            var nullObject = new JObject
            {
                ["klasseNavn"] = "N/A",
                ["uge"] = $"{GetIsoWeekNumber(date)}",
                ["indhold"] = "Der er ikke skrevet nogen ugenoter til denne uge",
            };
            weekLetter["ugebreve"] = new JArray(nullObject);

        }
        return weekLetter;
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
        if (_userProfile == null) throw new Exception("User profile not loaded");
        var kids = _userProfile["boern"];
        if (kids == null) throw new Exception("No children found in user profile");
        var id = "";
        foreach (var kid in kids)
            if (kid["fornavn"]?.ToString() == child.FirstName)
                id = kid["id"]?.ToString() ?? "";

        if (id == "") throw new Exception("Child not found");

        return id;
    }

    private int GetIsoWeekNumber(DateOnly date)
    {
        var cultureInfo = CultureInfo.CurrentCulture;
        var calendarWeekRule = cultureInfo.DateTimeFormat.CalendarWeekRule;
        var firstDayOfWeek = cultureInfo.DateTimeFormat.FirstDayOfWeek;
        return cultureInfo.Calendar.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue), calendarWeekRule, firstDayOfWeek);
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
            throw new Exception("No UserProfile found");

        var scriptText = script.InnerText;
        if (string.IsNullOrWhiteSpace(scriptText))
            throw new Exception("Script content is empty");

        var contextStart = "window.__tempcontext__['currentUser'] = ";
        var startIndex = scriptText.IndexOf(contextStart);
        if (startIndex == -1)
            throw new Exception("UserProfile context not found in script");

        startIndex += contextStart.Length;
        var endIndex = scriptText.IndexOf(";", startIndex);
        if (endIndex == -1 || endIndex <= startIndex)
            throw new Exception("Invalid UserProfile context format");

        var jsonText = scriptText.Substring(startIndex, endIndex - startIndex).Trim();
        if (string.IsNullOrWhiteSpace(jsonText))
            throw new Exception("Extracted JSON text is empty");

        try
        {
            return JObject.Parse(jsonText);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse UserProfile JSON: {ex.Message}");
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

    public async Task<JObject> GetWeekLetterWithFallback(Child child, DateOnly date)
    {
        var weekNumber = GetIsoWeekNumber(date);
        var year = date.Year;

        // Try to get live week letter first
        try
        {
            var liveWeekLetter = await GetWeekLetter(child, date);

            // Store it if we have Supabase service available
            if (_supabaseService != null)
            {
                try
                {
                    var contentHash = ComputeContentHash(liveWeekLetter.ToString());
                    await _supabaseService.StoreWeekLetterAsync(child.FirstName, weekNumber, year, contentHash, liveWeekLetter.ToString());
                    _logger?.LogInformation("Stored fresh week letter for {ChildName}, week {WeekNumber}/{Year}",
                        child.FirstName, weekNumber, year);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to store week letter for {ChildName}, week {WeekNumber}/{Year}",
                        child.FirstName, weekNumber, year);
                }
            }

            return liveWeekLetter;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch live week letter for {ChildName}, week {WeekNumber}/{Year}, trying stored version",
                child.FirstName, weekNumber, year);

            // Fallback to stored version
            var storedWeekLetter = await GetStoredWeekLetter(child, weekNumber, year);
            if (storedWeekLetter != null)
            {
                _logger?.LogInformation("Retrieved stored week letter for {ChildName}, week {WeekNumber}/{Year}",
                    child.FirstName, weekNumber, year);
                return storedWeekLetter;
            }

            // If no stored version, re-throw original exception
            throw;
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

    private string ComputeContentHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

    private JObject CreateEmptyWeekLetter(int weekNumber)
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