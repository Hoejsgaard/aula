using System.Net;
using System.Text;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Aula.Configuration;

namespace Aula.Integration;

/// <summary>
/// Handles authentication for children using pictogram-based login instead of passwords
/// </summary>
public class PictogramAuthenticatedClient : UniLoginDebugClient, IChildAuthenticatedClient
{
    private readonly Child _child;
    private readonly ILogger _logger;
    private readonly string[] _pictogramSequence;
    private readonly string _username;
    private string? _childId;

    public PictogramAuthenticatedClient(Child child, string username, string[] pictogramSequence, ILogger logger)
        : base(username, "", // Empty password since we'll build it dynamically
            "https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False",
            "https://www.minuddannelse.net/")
    {
        _child = child;
        _username = username;
        _pictogramSequence = pictogramSequence ?? throw new ArgumentNullException(nameof(pictogramSequence));
        _logger = logger;
    }

    public new async Task<bool> LoginAsync()
    {
        _logger.LogInformation("🖼️ Starting pictogram authentication for {ChildName} with sequence: {Sequence}",
            _child.FirstName, string.Join(" → ", _pictogramSequence));

        try
        {
            // Navigate through the login flow to reach the pictogram page
            var response = await HttpClient.GetAsync(
                "https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False");

            var content = await response.Content.ReadAsStringAsync();
            var maxSteps = 10;
            var hasSelectedUnilogin = false;
            var hasSubmittedUsername = false;

            for (var step = 0; step < maxSteps; step++)
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                // Step 1: Handle login selector page
                if (!hasSelectedUnilogin)
                {
                    var loginButtons = doc.DocumentNode.SelectNodes("//button[@name='selectedIdp']");
                    if (loginButtons != null)
                    {
                        _logger.LogDebug("📋 Found login selector page, selecting Unilogin");

                        var form = loginButtons[0].Ancestors("form").FirstOrDefault();
                        if (form != null)
                        {
                            var action = HttpUtility.HtmlDecode(form.GetAttributeValue("action", ""));
                            action = GetAbsoluteUrl(action, response.RequestMessage?.RequestUri?.ToString() ?? "");

                            var formData = new Dictionary<string, string> { ["selectedIdp"] = "uni_idp" };
                            response = await HttpClient.PostAsync(action, new FormUrlEncodedContent(formData));
                            content = await response.Content.ReadAsStringAsync();
                            hasSelectedUnilogin = true;
                            continue;
                        }
                    }
                }

                // Step 2: Submit username
                if (!hasSubmittedUsername)
                {
                    var usernameField = doc.DocumentNode.SelectSingleNode("//input[@name='username' and @type='text']");
                    if (usernameField != null)
                    {
                        _logger.LogDebug("📝 Submitting username: {Username}", _username);

                        var form = usernameField.Ancestors("form").FirstOrDefault();
                        if (form != null)
                        {
                            var action = HttpUtility.HtmlDecode(form.GetAttributeValue("action", ""));
                            action = GetAbsoluteUrl(action, response.RequestMessage?.RequestUri?.ToString() ?? "");

                            var formData = new Dictionary<string, string> { ["username"] = _username };
                            response = await HttpClient.PostAsync(action, new FormUrlEncodedContent(formData));
                            content = await response.Content.ReadAsStringAsync();
                            hasSubmittedUsername = true;
                            continue;
                        }
                    }
                }

                // Step 3: Handle pictogram authentication
                if (IsPictogramPage(doc))
                {
                    _logger.LogInformation("🎯 Reached pictogram authentication page");

                    // Parse the dynamic pictogram mapping
                    var pictogramMapping = ParsePictogramMapping(doc);
                    if (pictogramMapping.Count == 0)
                    {
                        _logger.LogError("❌ No pictograms found on page");
                        return false;
                    }

                    _logger.LogDebug("📊 Found {Count} pictograms: {Pictograms}",
                        pictogramMapping.Count, string.Join(", ", pictogramMapping.Keys));

                    // Build password from sequence
                    var password = BuildPasswordFromSequence(pictogramMapping, _pictogramSequence);
                    if (string.IsNullOrEmpty(password))
                    {
                        _logger.LogError("❌ Failed to build password from pictogram sequence");
                        return false;
                    }

                    _logger.LogDebug("🔑 Built password from pictogram sequence");

                    // Submit the form with the password
                    var success = await SubmitPictogramForm(doc, response, _username, password);
                    if (success)
                    {
                        _logger.LogInformation("✅ Successfully authenticated {ChildName} with pictograms!", _child.FirstName);

                        // Set the child ID after successful login
                        await SetChildIdAsync();
                        return true;
                    }
                    else
                    {
                        _logger.LogError("❌ Failed to submit pictogram form");
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

                    response = await HttpClient.PostAsync(action, new FormUrlEncodedContent(formData));
                    content = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    // No more forms, check if we're authenticated
                    if (response.RequestMessage?.RequestUri?.ToString().Contains("minuddannelse.net") ?? false)
                    {
                        _logger.LogInformation("✅ Reached MinUddannelse after authentication");
                        await SetChildIdAsync();
                        return true;
                    }
                }
            }

            _logger.LogError("❌ Failed to complete pictogram authentication after {Steps} steps", maxSteps);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception during pictogram authentication for {ChildName}", _child.FirstName);
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
                    _logger.LogTrace("🖼️ Pictogram: {Title} = {Value}", title, dataPassw);
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
                _logger.LogTrace("🔑 {Pictogram} → {Value}", pictogramName, value);
            }
            else
            {
                _logger.LogError("⚠️ Pictogram '{Pictogram}' not found in mapping! Available: {Available}",
                    pictogramName, string.Join(", ", mapping.Keys));
                return "";
            }
        }

        return passwordBuilder.ToString();
    }

    private async Task<bool> SubmitPictogramForm(HtmlDocument doc, HttpResponseMessage lastResponse, string username, string password)
    {
        // Find the form with hidden username/password fields
        var pictogramForm = doc.DocumentNode.SelectSingleNode("//input[@name='password' and @type='hidden']")?.Ancestors("form").FirstOrDefault();
        if (pictogramForm == null)
        {
            _logger.LogError("❌ No pictogram form found");
            return false;
        }

        var action = HttpUtility.HtmlDecode(pictogramForm.GetAttributeValue("action", ""));
        action = GetAbsoluteUrl(action, lastResponse.RequestMessage?.RequestUri?.ToString() ?? "");

        var formData = new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password
        };

        _logger.LogDebug("📤 Submitting pictogram form to: {Action}", action);

        var response = await HttpClient.PostAsync(action, new FormUrlEncodedContent(formData));
        var content = await response.Content.ReadAsStringAsync();

        // Check for success indicators
        if (content.Contains("MinUddannelse") || content.Contains("Dashboard") ||
            content.Contains("Forside") || content.Contains("Ugeplaner") ||
            content.Contains("SAMLResponse") ||
            (response.RequestMessage?.RequestUri?.ToString().Contains("minuddannelse.net") ?? false))
        {
            _logger.LogInformation("✅ Pictogram authentication successful!");
            return true;
        }
        else if (content.Contains("fejl") || content.Contains("error") || content.Contains("Der skete en fejl"))
        {
            _logger.LogError("❌ Login failed - error in response");

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

        // Sometimes there's another redirect after successful auth
        if (response.Headers.Location != null)
        {
            var redirectUrl = GetAbsoluteUrl(response.Headers.Location.ToString(), action);
            var finalResponse = await HttpClient.GetAsync(redirectUrl);
            var finalContent = await finalResponse.Content.ReadAsStringAsync();

            if (finalContent.Contains("MinUddannelse") ||
                (finalResponse.RequestMessage?.RequestUri?.ToString().Contains("minuddannelse.net") ?? false))
            {
                _logger.LogInformation("✅ Pictogram authentication successful after redirect!");
                return true;
            }
        }

        _logger.LogWarning("⚠️ Unknown response after pictogram submission");
        return false;
    }

    private string GetAbsoluteUrl(string url, string baseUrl)
    {
        if (string.IsNullOrEmpty(url)) return baseUrl;
        if (url.StartsWith("http://") || url.StartsWith("https://")) return url;

        var baseUri = new Uri(baseUrl);
        if (url.StartsWith("/"))
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

    private async Task SetChildIdAsync()
    {
        try
        {
            // Fetch the child ID from the API after successful login
            var apiUrl = $"https://www.minuddannelse.net/api/stamdata/elev/getElev?_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var response = await HttpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = Newtonsoft.Json.Linq.JObject.Parse(content);
                _childId = json["ChildId"]?.ToString();

                if (!string.IsNullOrEmpty(_childId))
                {
                    _logger.LogDebug("🆔 Set child ID: {ChildId} for {ChildName}", _childId, _child.FirstName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to fetch child ID, continuing anyway");
        }
    }

    public async Task<Newtonsoft.Json.Linq.JObject> GetWeekLetter(DateOnly date)
    {
        if (string.IsNullOrEmpty(_childId))
        {
            _logger.LogError("Child ID not available for {ChildName}", _child.FirstName);
            return new Newtonsoft.Json.Linq.JObject();
        }

        var url = $"https://www.minuddannelse.net/api/stamdata/ugeplan/getUgeBreve?tidspunkt={date.Year}-W{GetIsoWeekNumber(date)}" +
                 $"&elevId={_childId}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        _logger.LogDebug("📥 Fetching week letter from: {Url}", url);

        var response = await HttpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = Newtonsoft.Json.Linq.JObject.Parse(content);

            // Extract the actual content similar to ChildAuthenticatedClient
            if (json["UgePlan"] != null && json["UgePlan"]?["UgeBrevContent"] != null)
            {
                return (Newtonsoft.Json.Linq.JObject)(json["UgePlan"] ?? new Newtonsoft.Json.Linq.JObject());
            }
            return json;
        }

        _logger.LogError("❌ Failed to fetch week letter. Status: {Status}", response.StatusCode);
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
                 $"&tidspunkt={date.Year}-W{GetIsoWeekNumber(date)}&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        _logger.LogDebug("📅 Fetching schedule from: {Url}", url);

        var response = await HttpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return Newtonsoft.Json.Linq.JObject.Parse(content);
        }

        _logger.LogError("❌ Failed to fetch schedule. Status: {Status}", response.StatusCode);
        return new Newtonsoft.Json.Linq.JObject();
    }

    private static int GetIsoWeekNumber(DateOnly date)
    {
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        return cal.GetWeekOfYear(dateTime, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
}