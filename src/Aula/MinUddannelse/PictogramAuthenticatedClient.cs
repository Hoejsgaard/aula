using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Aula.Configuration;
using Aula.Content.WeekLetters;

namespace Aula.MinUddannelse;

/// <summary>
/// Handles authentication for children using pictogram-based login instead of passwords
/// </summary>
public sealed partial class PictogramAuthenticatedClient : UniLoginAuthenticatorBase, IChildAuthenticatedClient
{
    private readonly Child _child;
    private readonly ILogger _logger;
    private readonly string[] _pictogramSequence;
    private readonly string _username;
    private string? _childId;

    public PictogramAuthenticatedClient(Child child, string username, string[] pictogramSequence, ILogger logger, IHttpClientFactory httpClientFactory)
        : base(httpClientFactory, username, "", // Empty password since we'll build it dynamically
            "https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False",
            "https://www.minuddannelse.net/Node/",
            logger,
            "https://www.minuddannelse.net",
            "/api/stamdata/elev/getElev")
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(pictogramSequence);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _child = child;
        _username = username;
        _pictogramSequence = pictogramSequence;
        if (pictogramSequence.Length == 0)
            throw new ArgumentException("Pictogram sequence cannot be empty", nameof(pictogramSequence));
        _logger = logger;
    }

    public new async Task<bool> LoginAsync()
    {
        _logger.LogInformation("Starting pictogram authentication for {ChildName} with sequence: {Sequence}",
            _child.FirstName, string.Join(" → ", _pictogramSequence));

        try
        {
            using var httpClient = CreateHttpClient();
            // Navigate through the login flow to reach the pictogram page
            var response = await httpClient.GetAsync("https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False");

            var content = await response.Content.ReadAsStringAsync();
            var maxSteps = 10;
            var hasSelectedUnilogin = false;
            var hasSubmittedUsername = false;

            for (var step = 0; step < maxSteps; step++)
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                if (!hasSelectedUnilogin)
                {
                    var loginButtons = doc.DocumentNode.SelectNodes("//button[@name='selectedIdp']");
                    if (loginButtons != null)
                    {
                        _logger.LogDebug("Found login selector page, selecting Unilogin");

                        var form = loginButtons[0].Ancestors("form").FirstOrDefault();
                        if (form != null)
                        {
                            var action = HttpUtility.HtmlDecode(form.GetAttributeValue("action", ""));
                            action = GetAbsoluteUrl(action, response.RequestMessage?.RequestUri?.ToString() ?? "");

                            var formData = new Dictionary<string, string> { ["selectedIdp"] = "uni_idp" };
                            response = await httpClient.PostAsync(action, new FormUrlEncodedContent(formData));
                            content = await response.Content.ReadAsStringAsync();
                            hasSelectedUnilogin = true;
                            continue;
                        }
                    }
                }

                if (!hasSubmittedUsername)
                {
                    var usernameField = doc.DocumentNode.SelectSingleNode("//input[@name='username' and @type='text']");
                    if (usernameField != null)
                    {
                        _logger.LogDebug("Submitting username: {Username}", _username);

                        var form = usernameField.Ancestors("form").FirstOrDefault();
                        if (form != null)
                        {
                            var action = HttpUtility.HtmlDecode(form.GetAttributeValue("action", ""));
                            action = GetAbsoluteUrl(action, response.RequestMessage?.RequestUri?.ToString() ?? "");

                            var formData = new Dictionary<string, string> { ["username"] = _username };
                            response = await httpClient.PostAsync(action, new FormUrlEncodedContent(formData));
                            content = await response.Content.ReadAsStringAsync();
                            hasSubmittedUsername = true;
                            continue;
                        }
                    }
                }

                if (IsPictogramPage(doc))
                {
                    _logger.LogInformation("Reached pictogram authentication page");

                    // Parse the dynamic pictogram mapping
                    var pictogramMapping = ParsePictogramMapping(doc);
                    if (pictogramMapping.Count == 0)
                    {
                        _logger.LogError("No pictograms found on page");
                        return false;
                    }

                    _logger.LogDebug("Found {Count} pictograms: {Pictograms}",
                        pictogramMapping.Count, string.Join(", ", pictogramMapping.Keys));

                    // Build password from sequence
                    var password = BuildPasswordFromSequence(pictogramMapping, _pictogramSequence);
                    if (string.IsNullOrEmpty(password))
                    {
                        _logger.LogError("Failed to build password from pictogram sequence");
                        return false;
                    }

                    _logger.LogDebug("Built password from pictogram sequence");

                    // Submit the form with the password
                    var authenticated = await SubmitPictogramForm(httpClient, doc, response, _username, password);
                    if (authenticated)
                    {
                        _logger.LogInformation("Successfully authenticated {ChildName} with pictograms!", _child.FirstName);

                        // Add Accept header for JSON responses (critical for API calls!)
                        httpClient.DefaultRequestHeaders.Accept.Add(
                            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                        // Set the child ID after successful login
                        await SetChildIdAsync(httpClient);
                        return true;
                    }
                    else
                    {
                        _logger.LogError("Failed to complete pictogram authentication");
                        return false;
                    }
                }

                // Continue with next form if present
                var nextForm = doc.DocumentNode.SelectSingleNode("//form");
                if (nextForm != null)
                {
                    var action = HttpUtility.HtmlDecode(nextForm.GetAttributeValue("action", ""));
                    action = GetAbsoluteUrl(action, response.RequestMessage?.RequestUri?.ToString() ?? "");

                    // Extract all hidden fields
                    var formData = new Dictionary<string, string>();
                    var inputs = nextForm.SelectNodes(".//input");
                    if (inputs != null)
                    {
                        foreach (var input in inputs)
                        {
                            var name = input.GetAttributeValue("name", "");
                            var value = input.GetAttributeValue("value", "");
                            if (!string.IsNullOrEmpty(name))
                            {
                                formData[name] = value;
                            }
                        }
                    }

                    response = await httpClient.PostAsync(action, new FormUrlEncodedContent(formData));
                    content = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    // No more forms, check if we're authenticated
                    if (response.RequestMessage?.RequestUri?.ToString().Contains("minuddannelse.net") ?? false)
                    {
                        _logger.LogInformation("Reached MinUddannelse after authentication");

                        // Add Accept header for JSON responses (critical for API calls!)
                        if (!httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
                        {
                            httpClient.DefaultRequestHeaders.Accept.Add(
                                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                        }

                        await SetChildIdAsync(httpClient);
                        return true;
                    }
                }
            }

            _logger.LogError("Failed to complete pictogram authentication after {Steps} steps", maxSteps);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during pictogram authentication for {ChildName}", _child.FirstName);
            return false;
        }
    }

    private bool IsPictogramPage(HtmlDocument doc)
    {
        // Check for pictogram elements with data-passw attributes
        var pictograms = doc.DocumentNode.SelectNodes("//div[contains(@class, 'js-icon') and @data-passw]");

        // Check for hidden password field (not regular password input)
        var hiddenPasswordField = doc.DocumentNode.SelectSingleNode("//input[@name='password' and @type='hidden']");

        // Check for visual selection slots
        var selectionSlots = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'js-set-passw')]");

        return pictograms?.Count > 0 && hiddenPasswordField != null && selectionSlots != null;
    }

    private Dictionary<string, string> ParsePictogramMapping(HtmlDocument doc)
    {
        var mapping = new Dictionary<string, string>();

        // Find all pictogram elements - they have dynamic values!
        var pictograms = doc.DocumentNode.SelectNodes("//div[contains(@class, 'js-icon') and @data-passw]");
        if (pictograms == null)
        {
            // Try alternative selector
            pictograms = doc.DocumentNode.SelectNodes("//div[@class='password mb-4']//div[contains(@class, 'js-icon')]");
        }

        if (pictograms != null)
        {
            foreach (var pictogram in pictograms)
            {
                var title = pictogram.GetAttributeValue("title", "").ToLower();
                var dataPassw = pictogram.GetAttributeValue("data-passw", "");

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(dataPassw))
                {
                    mapping[title] = dataPassw;
                    _logger.LogTrace("Pictogram: {Title} = {Value}", title, dataPassw);
                }
            }
        }

        return mapping;
    }

    private string BuildPasswordFromSequence(Dictionary<string, string> mapping, string[] sequence)
    {
        var passwordBuilder = new StringBuilder();

        foreach (var pictogramName in sequence)
        {
            if (mapping.ContainsKey(pictogramName.ToLower()))
            {
                var value = mapping[pictogramName.ToLower()];
                passwordBuilder.Append(value);
                _logger.LogTrace("{Pictogram} → {Value}", pictogramName, value);
            }
            else
            {
                _logger.LogError("Pictogram '{Pictogram}' not found in mapping! Available: {Available}",
                    pictogramName, string.Join(", ", mapping.Keys));
                return "";
            }
        }

        return passwordBuilder.ToString();
    }

    private async Task<bool> SubmitPictogramForm(HttpClient httpClient, HtmlDocument doc, HttpResponseMessage lastResponse, string username, string password)
    {
        // Find the form with hidden username/password fields
        var pictogramForm = doc.DocumentNode.SelectSingleNode("//input[@name='password' and @type='hidden']")?.Ancestors("form").FirstOrDefault();
        if (pictogramForm == null)
        {
            _logger.LogError("No pictogram form found");
            return false;
        }

        var action = HttpUtility.HtmlDecode(pictogramForm.GetAttributeValue("action", ""));
        action = GetAbsoluteUrl(action, lastResponse.RequestMessage?.RequestUri?.ToString() ?? "");

        var formData = new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password
        };

        _logger.LogDebug("Submitting pictogram form to: {Action}", action);

        var response = await httpClient.PostAsync(action, new FormUrlEncodedContent(formData));
        var content = await response.Content.ReadAsStringAsync();

        // Check for error indicators first
        if (content.Contains("fejl") || content.Contains("error") || content.Contains("Der skete en fejl"))
        {
            _logger.LogError("Login failed - error in response");

            // Try to extract error message
            var errorDoc = new HtmlDocument();
            errorDoc.LoadHtml(content);
            var errorMsg = errorDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'alert-error')]")?.InnerText?.Trim();
            if (!string.IsNullOrEmpty(errorMsg))
            {
                _logger.LogError("Error message: {Error}", errorMsg);
            }

            return false;
        }

        // Handle SAML response form if present
        if (content.Contains("SAMLResponse"))
        {
            _logger.LogDebug("Found SAML response, processing...");

            var samlDoc = new HtmlDocument();
            samlDoc.LoadHtml(content);

            // Look for the SAML response form
            var samlForm = samlDoc.DocumentNode.SelectSingleNode("//input[@name='SAMLResponse']")?.Ancestors("form").FirstOrDefault();
            if (samlForm != null)
            {
                var samlAction = HttpUtility.HtmlDecode(samlForm.GetAttributeValue("action", ""));
                samlAction = GetAbsoluteUrl(samlAction, response.RequestMessage?.RequestUri?.ToString() ?? "");

                // Extract all form fields for SAML submission
                var samlFormData = new Dictionary<string, string>();
                var inputs = samlForm.SelectNodes(".//input");
                if (inputs != null)
                {
                    foreach (var input in inputs)
                    {
                        var name = input.GetAttributeValue("name", "");
                        var value = input.GetAttributeValue("value", "");
                        if (!string.IsNullOrEmpty(name))
                        {
                            samlFormData[name] = value;
                        }
                    }
                }

                _logger.LogDebug("Submitting SAML response to: {Action}", samlAction);

                // Submit the SAML response
                response = await httpClient.PostAsync(samlAction, new FormUrlEncodedContent(samlFormData));
                content = await response.Content.ReadAsStringAsync();

                // Continue processing any additional forms/redirects
                var maxRedirects = 5;
                for (int i = 0; i < maxRedirects; i++)
                {
                    // Check if we've reached MinUddannelse
                    if (response.RequestMessage?.RequestUri?.ToString().Contains("minuddannelse.net") ?? false)
                    {
                        _logger.LogInformation("Successfully reached MinUddannelse!");

                        // Add Accept header for JSON responses (critical for API calls!)
                        if (!httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
                        {
                            httpClient.DefaultRequestHeaders.Accept.Add(
                                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                        }

                        return true;
                    }

                    // Check for additional forms to submit
                    var nextDoc = new HtmlDocument();
                    nextDoc.LoadHtml(content);
                    var nextForm = nextDoc.DocumentNode.SelectSingleNode("//form");

                    if (nextForm != null && !nextForm.InnerHtml.Contains("username") && !nextForm.InnerHtml.Contains("password"))
                    {
                        var nextAction = HttpUtility.HtmlDecode(nextForm.GetAttributeValue("action", ""));
                        nextAction = GetAbsoluteUrl(nextAction, response.RequestMessage?.RequestUri?.ToString() ?? "");

                        var nextFormData = new Dictionary<string, string>();
                        var nextInputs = nextForm.SelectNodes(".//input");
                        if (nextInputs != null)
                        {
                            foreach (var input in nextInputs)
                            {
                                var name = input.GetAttributeValue("name", "");
                                var value = input.GetAttributeValue("value", "");
                                if (!string.IsNullOrEmpty(name))
                                {
                                    nextFormData[name] = value;
                                }
                            }
                        }

                        _logger.LogDebug("Following redirect to: {Action}", nextAction);
                        response = await httpClient.PostAsync(nextAction, new FormUrlEncodedContent(nextFormData));
                        content = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        // Check for success after all processing
        if (content.Contains("MinUddannelse") || content.Contains("Dashboard") ||
            content.Contains("Forside") || content.Contains("Ugeplaner") ||
            (response.RequestMessage?.RequestUri?.ToString().Contains("minuddannelse.net") ?? false))
        {
            _logger.LogInformation("Pictogram authentication successful!");
            return true;
        }

        // Handle Location header redirects
        if (response.Headers.Location != null)
        {
            var redirectUrl = GetAbsoluteUrl(response.Headers.Location.ToString(), action);
            var finalResponse = await httpClient.GetAsync(redirectUrl);
            var finalContent = await finalResponse.Content.ReadAsStringAsync();

            if (finalContent.Contains("MinUddannelse") ||
                (finalResponse.RequestMessage?.RequestUri?.ToString().Contains("minuddannelse.net") ?? false))
            {
                _logger.LogInformation("Pictogram authentication successful after redirect!");
                return true;
            }
        }

        _logger.LogWarning("Unknown response after pictogram submission");
        return false;
    }

    private string GetAbsoluteUrl(string url, string baseUrl)
    {
        if (string.IsNullOrEmpty(url)) return baseUrl;
        if (url.StartsWith("http://") || url.StartsWith("https://")) return url;

        var baseUri = new Uri(baseUrl);
        if (url.StartsWith('/'))
        {
            return $"{baseUri.Scheme}://{baseUri.Host}{url}";
        }
        else
        {
            var basePath = baseUri.AbsolutePath;
            var lastSlash = basePath.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                basePath = basePath.Substring(0, lastSlash + 1);
            }
            return $"{baseUri.Scheme}://{baseUri.Host}{basePath}{url}";
        }
    }

    private async Task SetChildIdAsync(HttpClient httpClient)
    {
        try
        {
            _logger.LogInformation("Extracting child ID for {ChildName}...", _child.FirstName);

            // First try: Extract from page context (same method as ChildAuthenticatedClient)
            // Navigate to any MinUddannelse page after authentication to get this
            var response = await httpClient.GetAsync("https://www.minuddannelse.net/node/minuge");
            var content = await response.Content.ReadAsStringAsync();

            var personIdMatch = PersonIdRegex().Match(content);
            if (personIdMatch.Success)
            {
                _childId = personIdMatch.Groups[1].Value;
                _logger.LogInformation("Extracted child ID from page context: {ChildId}", _childId);

                var nameMatch = NameRegex().Match(content);
                if (nameMatch.Success)
                {
                    var firstName = nameMatch.Groups[1].Value;
                    var lastName = nameMatch.Groups[2].Value;
                    _logger.LogInformation("Confirmed authenticated as: {FirstName} {LastName}",
                        firstName, lastName);
                }

                return;
            }

            // Fallback: Try the API method (improved version)
            _logger.LogInformation("Page context method failed, trying API...");
            var apiUrl = $"https://www.minuddannelse.net/api/stamdata/elev/getElev?_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            var apiResponse = await httpClient.GetAsync(apiUrl);
            if (apiResponse.IsSuccessStatusCode)
            {
                var apiContent = await apiResponse.Content.ReadAsStringAsync();
                if (apiContent.StartsWith('{') || apiContent.StartsWith('['))
                {
                    var studentData = Newtonsoft.Json.Linq.JObject.Parse(apiContent);
                    _childId = studentData["id"]?.ToString() ??
                              studentData["elevId"]?.ToString() ??
                              studentData["personid"]?.ToString();

                    if (!string.IsNullOrEmpty(_childId))
                    {
                        _logger.LogInformation("Extracted child ID from API: {ChildId}", _childId);
                        return;
                    }
                }
            }

            _logger.LogWarning("Could not extract child ID from any source");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting child ID");
        }
    }

    public async Task<Newtonsoft.Json.Linq.JObject> GetWeekLetter(DateOnly date)
    {
        if (string.IsNullOrEmpty(_childId))
        {
            _logger.LogError("Child ID not available for {ChildName}", _child.FirstName);
            return new Newtonsoft.Json.Linq.JObject();
        }

        var url = $"https://www.minuddannelse.net/api/elev/ugeplan/getUgeplan?tidspunkt={date.Year}-W{WeekLetterUtilities.GetIsoWeekNumber(date)}" +
                 $"&elevId={_childId}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        _logger.LogDebug("Fetching week letter from: {Url}", url);

        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();

            // Check if content is HTML (error page) instead of JSON
            if (content.TrimStart().StartsWith('<'))
            {
                _logger.LogWarning("Received HTML instead of JSON for week letter. Might be an authentication or session issue.");
                _logger.LogDebug("Response content starts with: {Content}", content.Substring(0, Math.Min(100, content.Length)));

                // Return empty week letter with appropriate message
                return new Newtonsoft.Json.Linq.JObject
                {
                    ["errorMessage"] = "No week letter available",
                    ["ugebreve"] = new Newtonsoft.Json.Linq.JArray()
                };
            }

            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(content);

                // Extract the actual content similar to ChildAuthenticatedClient
                if (json["UgePlan"] != null && json["UgePlan"]?["UgeBrevContent"] != null)
                {
                    return (Newtonsoft.Json.Linq.JObject)(json["UgePlan"] ?? new Newtonsoft.Json.Linq.JObject());
                }
                return json;
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON response for week letter");
                _logger.LogDebug("Response content: {Content}", content.Substring(0, Math.Min(500, content.Length)));

                // Return empty week letter
                return new Newtonsoft.Json.Linq.JObject
                {
                    ["errorMessage"] = "Failed to parse week letter response",
                    ["ugebreve"] = new Newtonsoft.Json.Linq.JArray()
                };
            }
        }

        _logger.LogError("Failed to fetch week letter. Status: {Status}", response.StatusCode);
        return new Newtonsoft.Json.Linq.JObject();
    }

    public async Task<Newtonsoft.Json.Linq.JObject> GetWeekSchedule(DateOnly date)
    {
        if (string.IsNullOrEmpty(_childId))
        {
            _logger.LogError("Child ID not available for {ChildName}", _child.FirstName);
            return new Newtonsoft.Json.Linq.JObject();
        }

        var url = $"https://www.minuddannelse.net/api/stamdata/aulaskema/getElevSkema?elevId={_childId}" +
                 $"&tidspunkt={date.Year}-W{WeekLetterUtilities.GetIsoWeekNumber(date)}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        _logger.LogDebug("Fetching schedule from: {Url}", url);

        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return Newtonsoft.Json.Linq.JObject.Parse(content);
        }

        _logger.LogError("Failed to fetch schedule. Status: {Status}", response.StatusCode);
        return new Newtonsoft.Json.Linq.JObject();
    }

    [GeneratedRegex(@"""personid"":(\d+)")]
    private static partial Regex PersonIdRegex();

    [GeneratedRegex(@"""fornavn"":""([^""]*)"",""efternavn"":""([^""]*)""")]
    private static partial Regex NameRegex();
}
