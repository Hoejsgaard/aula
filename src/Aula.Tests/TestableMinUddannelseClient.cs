using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Configuration;

namespace Aula.Tests;

/// <summary>
/// A testable version of MinUddannelseClient that allows injecting an HttpClient for testing
/// </summary>
public class TestableMinUddannelseClient : IMinUddannelseClient
{
    private readonly HttpClient _httpClient;
    private readonly string _username;
    private readonly string _password;
    private JObject _userProfile = new();
    private bool _loggedIn;

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

        var url = $"https://www.minuddannelse.net/api/stamdata/ugeplan/getUgeBreve?tidspunkt={date.Year}-W{GetIsoWeekNumber(date)}&elevId=123&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        return JObject.Parse(json);
    }

    public async Task<JObject> GetWeekSchedule(Child child, DateOnly date)
    {
        if (!_loggedIn)
            throw new InvalidOperationException("Not logged in");

        var url = $"https://www.minuddannelse.net/api/stamdata/aulaskema/getElevSkema?elevId=123&tidspunkt={date.Year}-W{GetIsoWeekNumber(date)}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        return JObject.Parse(json);
    }

    private int GetIsoWeekNumber(DateOnly date)
    {
        var cultureInfo = System.Globalization.CultureInfo.CurrentCulture;
        var calendarWeekRule = cultureInfo.DateTimeFormat.CalendarWeekRule;
        var firstDayOfWeek = cultureInfo.DateTimeFormat.FirstDayOfWeek;
        return cultureInfo.Calendar.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue), calendarWeekRule, firstDayOfWeek);
    }
}