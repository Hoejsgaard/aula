using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Configuration;

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

    public Task<bool> LoginAsync()
    {
        // In tests, we'll just simulate a successful login
        _loggedIn = true;

        // Set up a mock user profile for testing
        _userProfile = new JObject
        {
            ["boern"] = new JArray
            {
                new JObject
                {
                    ["id"] = "123",
                    ["fornavn"] = "Test",
                    ["efternavn"] = "Child"
                }
            }
        };

        return Task.FromResult(true);
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
        // In tests, we'll use the first name as a simple ID mapping
        // This matches the mock user profile we set up in LoginAsync
        if (_userProfile["boern"] is JArray children)
        {
            foreach (var kid in children)
            {
                if (kid["fornavn"]?.ToString() == child.FirstName)
                {
                    return kid["id"]?.ToString() ?? "123";
                }
            }
        }
        return "123"; // Default fallback for tests
    }

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