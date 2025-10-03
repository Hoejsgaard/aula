using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using MinUddannelse.Configuration;
using MinUddannelse.Content.WeekLetters;

namespace MinUddannelse.Client;

public sealed partial class ChildAuthenticatedClient : UniLoginAuthenticatorBase, IChildAuthenticatedClient
{
    private readonly Child _child;
    private readonly ILogger _logger;
    private readonly string _username;
    private string? _childId;

    public ChildAuthenticatedClient(Child child, string username, string password, ILogger logger, IHttpClientFactory httpClientFactory)
        : base(httpClientFactory, username, password,
            "https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False",
            "https://www.minuddannelse.net/Node/",
            logger,
            "https://www.minuddannelse.net",
            "/api/stamdata/elev/getElev")
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        _child = child;
        _username = username;
        _logger = logger;
    }

    public new async Task<bool> LoginAsync()
    {
        _logger.LogInformation("Attempting to login for {ChildName} at URL: https://www.minuddannelse.net",
            _child.FirstName);

        var loginSuccess = await base.LoginAsync();

        _logger.LogInformation("Base login returned: {Success}", loginSuccess);

        if (loginSuccess)
        {
            _logger.LogInformation("Attempting to extract child ID for {ChildName}", _child.FirstName);

            _childId = await ExtractChildId();

            if (string.IsNullOrEmpty(_childId))
            {
                _logger.LogError("Could not extract child ID for {ChildName}", _child.FirstName);
                return false;
            }

            _logger.LogInformation("Extracted child ID for {ChildName}: {ChildId}", _child.FirstName, _childId);
            return true;
        }

        _logger.LogError("Base login failed for {ChildName}", _child.FirstName);
        return false;
    }

    private async Task<string?> ExtractChildId()
    {
        try
        {
            _logger.LogInformation("Extracting child ID for {ChildName}", _child.FirstName);

            using var httpClient = CreateHttpClient();
            var response = await httpClient.GetAsync("https://www.minuddannelse.net/node/minuge");
            var content = await response.Content.ReadAsStringAsync();

            var personIdMatch = PersonIdRegex().Match(content);
            if (personIdMatch.Success)
            {
                var childId = personIdMatch.Groups[1].Value;
                _logger.LogInformation("Extracted child ID from page context: {ChildId}", childId);

                var nameMatch = NameRegex().Match(content);
                if (nameMatch.Success)
                {
                    var firstName = nameMatch.Groups[1].Value;
                    var lastName = nameMatch.Groups[2].Value;
                    _logger.LogInformation("Confirmed authenticated as: {FirstName} {LastName}",
                        firstName, lastName);
                }

                return childId;
            }

            _logger.LogInformation("Page context method failed, trying API");
            var apiUrl = $"https://www.minuddannelse.net/api/stamdata/elev/getElev?_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            var apiResponse = await httpClient.GetAsync(apiUrl);
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
                        _logger.LogInformation("Extracted child ID from API: {ChildId}", childId);
                        return childId;
                    }
                }
            }

            _logger.LogWarning("Could not extract child ID from any source");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting child ID");
            return null;
        }
    }

    public async Task<JObject> GetWeekLetter(DateOnly date)
    {
        if (string.IsNullOrEmpty(_childId))
        {
            _logger.LogError("Child ID not available for {ChildName}", _child.FirstName);
            return new JObject();
        }

        var url = $"https://www.minuddannelse.net/api/elev/ugeplan/getUgeplan?tidspunkt={date.Year}-W{WeekLetterUtilities.GetIsoWeekNumber(date)}" +
                 $"&elevId={_childId}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        // Clean up smart quotes and other problematic Unicode characters that break JSON parsing
        var cleanedJson = json
            .Replace("\u201E", "\\\"")  // Opening smart quote „
            .Replace("\u201C", "\\\"")  // Closing smart quote "
            .Replace("\u201D", "\\\"")  // Another closing smart quote "
            .Replace("\u2019", "\\'")   // Smart apostrophe '
            .Replace("\u2018", "\\'");  // Another smart apostrophe '

        var weekLetter = JObject.Parse(cleanedJson);
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
            _logger.LogError("Child ID not available for {ChildName}", _child.FirstName);
            return new JObject();
        }

        var url = $"https://www.minuddannelse.net/api/stamdata/aulaskema/getElevSkema?elevId={_childId}" +
                 $"&tidspunkt={date.Year}-W{WeekLetterUtilities.GetIsoWeekNumber(date)}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JObject.Parse(json);
    }

    [GeneratedRegex(@"""personid"":(\d+)")]
    private static partial Regex PersonIdRegex();

    [GeneratedRegex(@"""fornavn"":""([^""]*)"",""efternavn"":""([^""]*)""")]
    private static partial Regex NameRegex();
}
