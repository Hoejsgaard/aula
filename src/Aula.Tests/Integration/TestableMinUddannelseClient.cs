using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Configuration;
using HtmlAgilityPack;

namespace Aula.Tests.Integration;

/// <summary>
/// A testable version of MinUddannelseClient that allows injecting an HttpClient for testing
/// </summary>
public class TestableMinUddannelseClient : IMinUddannelseClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _username;
    private readonly string _password;
    private JObject _userProfile = new();
    private bool _loggedIn;
    private bool _disposed;

    public TestableMinUddannelseClient(HttpClient httpClient, string username, string password)
    {
        _httpClient = httpClient;
        _username = username;
        _password = password;
    }

    public async Task<bool> LoginAsync()
    {
        // In tests, we'll simulate a successful login and profile extraction
        _loggedIn = true;

        // Try to extract user profile from the HTTP response
        _userProfile = await ExtractUserProfile();

        return true;
    }

    private async Task<JObject> ExtractUserProfile()
    {
        var response = await _httpClient.GetAsync("https://www.minuddannelse.net/Node/");
        var content = await response.Content.ReadAsStringAsync();
        
        // Mimic the real MinUddannelseClient logic exactly
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(content);

        // Find the script node that contains __tempcontext__
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

    public async Task<JObject> GetWeekLetter(Child child, DateOnly date)
    {
        if (!_loggedIn)
            throw new InvalidOperationException("Not logged in");

        var url = $"https://www.minuddannelse.net/api/stamdata/ugeplan/getUgeBreve?tidspunkt={date.Year}-W{GetIsoWeekNumber(date)}&elevId={GetChildId(child)}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        var weekLetter = JObject.Parse(json);
        var weekLetterArray = weekLetter["ugebreve"] as JArray;

        // Mimic the real MinUddannelseClient behavior for empty arrays
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
        if (!_loggedIn)
            throw new InvalidOperationException("Not logged in");

        var url = $"https://www.minuddannelse.net/api/stamdata/aulaskema/getElevSkema?elevId={GetChildId(child)}&tidspunkt={date.Year}-W{GetIsoWeekNumber(date)}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        return JObject.Parse(json);
    }

    private string GetChildId(Child child)
    {
        if (_userProfile == null || !_userProfile.HasValues) throw new Exception("User profile not loaded");
        var kids = _userProfile["boern"];
        if (kids == null) throw new Exception("No children found in user profile");
        var id = "";
        foreach (var kid in kids)
            if (kid["fornavn"]?.ToString() == child.FirstName)
                id = kid["id"]?.ToString() ?? "";

        if (id == "") throw new Exception("Child not found");

        return id;
    }

    // Test helper methods to expose private functionality
    public string TestGetChildId(Child child) => GetChildId(child);
    public int TestGetIsoWeekNumber(DateOnly date) => GetIsoWeekNumber(date);

    private int GetIsoWeekNumber(DateOnly date)
    {
        // ISO 8601 week calculation
        var dt = date.ToDateTime(TimeOnly.MinValue);
        var day = (int)System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(dt);
        return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(dt.AddDays(4 - (day == 0 ? 7 : day)), System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}