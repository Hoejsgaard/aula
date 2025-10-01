using System.Net;
using System.Net.Http.Headers;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Aula.Configuration;

namespace Aula.Integration;

/// <summary>
/// Base authenticator for UniLogin SAML-based authentication
/// Handles the multi-step form submission flow with detailed logging
/// </summary>
public abstract class UniLoginAuthenticatorBase : IDisposable
{
    private bool _disposed = false;
    private readonly ILogger _logger;
    private readonly string _loginUrl;
    private readonly string _password;
    private readonly string _username;
    private readonly string _apiBaseUrl;
    private readonly string _studentDataPath;
    private bool _loggedIn;

    public UniLoginAuthenticatorBase(string username, string password, string loginUrl, string successUrl, ILogger logger, string apiBaseUrl = "https://www.minuddannelse.net", string studentDataPath = "/api/stamdata/elev/getElev")
    {
        var httpClientHandler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = true
        };
        HttpClient = new HttpClient(httpClientHandler);
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _loginUrl = loginUrl;
        SuccessUrl = successUrl;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _apiBaseUrl = apiBaseUrl;
        _studentDataPath = studentDataPath;
    }

    protected HttpClient HttpClient { get; }
    protected string SuccessUrl { get; }

    public async Task<bool> LoginAsync()
    {
        _logger.LogDebug("Starting login process for user: {Username}", _username);
        _logger.LogDebug("Initial URL: {LoginUrl}", _loginUrl);

        var response = await HttpClient.GetAsync(_loginUrl);
        var content = await response.Content.ReadAsStringAsync();

        _logger.LogDebug("Initial response status: {StatusCode}", response.StatusCode);
        _logger.LogDebug("Response URL: {ResponseUri}", response.RequestMessage?.RequestUri);

        return await ProcessLoginResponseAsync(content, response);
    }

    private async Task<bool> ProcessLoginResponseAsync(string content, HttpResponseMessage initialResponse)
    {
        var maxSteps = 10;
        var currentUrl = initialResponse.RequestMessage?.RequestUri?.ToString() ?? _loginUrl;
        var hasSubmittedCredentials = false;

        for (var stepCounter = 0; stepCounter < maxSteps; stepCounter++)
        {
            _logger.LogDebug("===== STEP {StepCounter} =====", stepCounter);
            _logger.LogDebug("Current URL: {CurrentUrl}", currentUrl);
            _logger.LogDebug("Content length: {ContentLength} chars", content.Length);

            // Track if we've submitted credentials
            if (currentUrl.Contains("broker.unilogin.dk") && content.Contains("password"))
            {
                hasSubmittedCredentials = true;
                _logger.LogDebug("Credentials form detected");
            }

            // Check if we're authenticated after returning from credential submission
            if (await TryVerifyAuthenticationAfterCredentials(currentUrl, hasSubmittedCredentials))
            {
                return true;
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                // Log page title
                var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
                _logger.LogDebug("Page title: {Title}", title ?? "No title found");

                // Log form structure for debugging
                LogFormStructure(doc);

                // Check for authentication success indicators
                if (CheckForAuthenticationSuccess(doc, currentUrl, stepCounter))
                {
                    return true;
                }

                // Try to submit form if found
                var (authenticated, newHasSubmittedCredentials, newContent, newUrl) =
                    await TrySubmitForm(content, currentUrl, hasSubmittedCredentials);

                if (authenticated)
                {
                    return true;
                }

                if (newContent != content || newUrl != currentUrl)
                {
                    content = newContent;
                    currentUrl = newUrl;
                    hasSubmittedCredentials = newHasSubmittedCredentials;
                    continue;
                }

                // Try alternative navigation (UniLogin link)
                var (navigated, navContent, navUrl) = await TryAlternativeNavigation(doc, currentUrl);
                if (navigated)
                {
                    content = navContent;
                    currentUrl = navUrl;
                    continue;
                }

                // Check for errors and exit if no progress possible
                LogErrorMessages(doc);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step {StepCounter} failed with exception", stepCounter);
            }
        }

        _logger.LogWarning("Login failed after {MaxSteps} steps", maxSteps);
        return false;
    }

    private void LogFormStructure(HtmlDocument doc)
    {
        var forms = doc.DocumentNode.SelectNodes("//form");
        if (forms != null)
        {
            _logger.LogDebug("Found {FormCount} form(s) on page", forms.Count);
            foreach (var form in forms)
            {
                var formAction = form.Attributes["action"]?.Value;
                var formMethod = form.Attributes["method"]?.Value;
                var formId = form.Attributes["id"]?.Value;
                var formName = form.Attributes["name"]?.Value;
                _logger.LogDebug("Form: action='{FormAction}', method='{FormMethod}', id='{FormId}', name='{FormName}'",
                    formAction, formMethod, formId, formName);

                var inputs = form.SelectNodes(".//input");
                if (inputs != null)
                {
                    foreach (var input in inputs)
                    {
                        var inputName = input.Attributes["name"]?.Value;
                        var inputType = input.Attributes["type"]?.Value;
                        var inputId = input.Attributes["id"]?.Value;
                        if (inputType != "hidden")
                        {
                            _logger.LogDebug("Input: name='{InputName}', type='{InputType}', id='{InputId}'",
                                inputName, inputType, inputId);
                        }
                    }
                }
            }
        }
        else
        {
            _logger.LogDebug("No forms found on this page");
            LogJavaScriptRedirects(doc);
            LogLoginLinks(doc);
            LogMetaRefresh(doc);
        }
    }

    private void LogJavaScriptRedirects(HtmlDocument doc)
    {
        var scripts = doc.DocumentNode.SelectNodes("//script");
        if (scripts != null)
        {
            foreach (var script in scripts)
            {
                if (script.InnerText.Contains("window.location") ||
                    script.InnerText.Contains("redirect") ||
                    script.InnerText.Contains("submit"))
                {
                    _logger.LogDebug("Found potential JavaScript redirect/submit");
                    var scriptPreview = script.InnerText.Length > 200
                        ? script.InnerText.Substring(0, 200) + "..."
                        : script.InnerText;
                    _logger.LogDebug("Script preview: {ScriptPreview}", scriptPreview);
                }
            }
        }
    }

    private void LogLoginLinks(HtmlDocument doc)
    {
        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, 'login') or contains(@href, 'Login') or contains(@href, 'auth')]");
        if (links != null)
        {
            _logger.LogDebug("Found {LinkCount} login-related link(s)", links.Count);
            foreach (var link in links)
            {
                var href = link.Attributes["href"]?.Value;
                var text = link.InnerText?.Trim();
                _logger.LogDebug("Link: href='{Href}', text='{Text}'", href, text);
            }
        }
    }

    private void LogMetaRefresh(HtmlDocument doc)
    {
        var metaRefresh = doc.DocumentNode.SelectSingleNode("//meta[@http-equiv='refresh']");
        if (metaRefresh != null)
        {
            var refreshContent = metaRefresh.Attributes["content"]?.Value;
            _logger.LogDebug("Meta refresh found: {RefreshContent}", refreshContent);
        }
    }

    private void LogErrorMessages(HtmlDocument doc)
    {
        var errorElements = doc.DocumentNode.SelectNodes("//*[contains(@class, 'error') or contains(@class, 'alert')]");
        if (errorElements != null)
        {
            foreach (var error in errorElements)
            {
                _logger.LogWarning("Possible error message: {ErrorMessage}", error.InnerText?.Trim());
            }
        }

        var loginButton = doc.DocumentNode.SelectSingleNode("//button[contains(text(), 'Log') or contains(text(), 'Uni')]");
        if (loginButton != null)
        {
            _logger.LogDebug("Found login button: {ButtonText}", loginButton.InnerText);
        }
    }

    private Tuple<string, Dictionary<string, string>>? ExtractFormData(string htmlContent)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Try to find a login form specifically
            var formNode = doc.DocumentNode.SelectSingleNode("//form[contains(@action, 'login') or contains(@action, 'Login') or contains(@action, 'auth')]")
                          ?? doc.DocumentNode.SelectSingleNode("//form");

            if (formNode == null)
            {
                _logger.LogDebug("No form found for data extraction");
                return null;
            }

            var actionUrl = formNode.Attributes["action"]?.Value;
            if (actionUrl == null)
            {
                _logger.LogDebug("Form has no action attribute");
                return null;
            }

            var formData = BuildFormData(doc, formNode);
            var writer = new StringWriter();
            HttpUtility.HtmlDecode(actionUrl, writer);
            var decodedUrl = writer.ToString();

            // Make URL absolute if needed
            if (!decodedUrl.StartsWith("http"))
            {
                var baseUri = new Uri(_loginUrl);
                var absoluteUri = new Uri(baseUri, decodedUrl);
                decodedUrl = absoluteUri.ToString();
            }

            return new Tuple<string, Dictionary<string, string>>(decodedUrl, formData);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ExtractFormData exception");
            return null;
        }
    }

    private Dictionary<string, string> BuildFormData(HtmlDocument document, HtmlNode formNode)
    {
        var formData = new Dictionary<string, string>();

        // Get inputs from the specific form
        var inputs = formNode.SelectNodes(".//input");
        if (inputs == null)
        {
            _logger.LogDebug("No inputs found in form");
            formData.Add("selectedIdp", "uni_idp");
            return formData;
        }

        foreach (var input in inputs)
        {
            var name = input.Attributes["name"]?.Value;
            var value = input.GetAttributeValue("value", string.Empty);
            var type = input.GetAttributeValue("type", "text");

            if (string.IsNullOrWhiteSpace(name)) continue;

            // Log what we're adding to form data
            if (type != "hidden")
            {
                _logger.LogDebug("Adding to form: {FieldName} = {Value}", name,
                    name.Contains("password", StringComparison.OrdinalIgnoreCase) ? "***" : value);
            }

            // Handle various field names for username and password
            var lowerName = name.ToLowerInvariant();
            if (lowerName == "username" || lowerName == "j_username" || lowerName == "user" || lowerName == "login")
            {
                formData[name] = _username;
                _logger.LogDebug("Setting username field '{FieldName}' to: {Username}", name, _username);
            }
            else if (lowerName == "password" || lowerName == "j_password" || lowerName == "pass" || lowerName == "pwd")
            {
                formData[name] = _password;
                _logger.LogDebug("Setting password field '{FieldName}' to: ***", name);
            }
            else
            {
                formData[name] = value;
            }
        }

        // Also check for select elements
        var selects = formNode.SelectNodes(".//select");
        if (selects != null)
        {
            foreach (var select in selects)
            {
                var name = select.Attributes["name"]?.Value;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    // Get selected option or first option
                    var selectedOption = select.SelectSingleNode(".//option[@selected]")
                                      ?? select.SelectSingleNode(".//option");
                    if (selectedOption != null)
                    {
                        var value = selectedOption.Attributes["value"]?.Value ?? "";
                        formData[name] = value;
                        _logger.LogDebug("Adding select to form: {FieldName} = {Value}", name, value);
                    }
                }
            }
        }

        return formData;
    }

    private bool CheckIfLoginSuccessful(HttpResponseMessage response)
    {
        _loggedIn = response.RequestMessage?.RequestUri?.ToString().Contains(SuccessUrl.Replace("https://", "").Replace("http://", "")) ?? false;

        if (_loggedIn)
        {
            _logger.LogDebug("Login check: SUCCESS - URL matches success pattern");
        }
        else
        {
            _logger.LogDebug("Login check: Current URL doesn't match success URL");
        }

        return _loggedIn;
    }

    private async Task<bool> VerifyAuthentication()
    {
        _logger.LogDebug("Verifying authentication status");

        // Try multiple endpoints to verify authentication
        var endpoints = new[]
        {
            $"{_apiBaseUrl}{_studentDataPath}",
            $"{_apiBaseUrl}/api/stamdata/getProfiles",
            $"{_apiBaseUrl}/api/stamdata/bruger/getBruger"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var url = endpoint + "?_=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _logger.LogDebug("Testing endpoint: {Endpoint}", endpoint);

                var response = await HttpClient.GetAsync(url);
                _logger.LogDebug("Response: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("Content length: {ContentLength} chars", content.Length);

                    // Check if we got actual JSON data (not error page)
                    if (content.StartsWith('{') || content.StartsWith('['))
                    {
                        _logger.LogDebug("Valid JSON response received");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error testing endpoint");
            }
        }

        _logger.LogWarning("All authentication verification endpoints failed");
        return false;
    }

    private async Task<bool> TryVerifyAuthenticationAfterCredentials(string currentUrl, bool hasSubmittedCredentials)
    {
        if (!hasSubmittedCredentials || !currentUrl.Contains("minuddannelse.net") || currentUrl.Contains("Login"))
        {
            return false;
        }

        _logger.LogDebug("Back at MinUddannelse after credential submission");
        var authenticated = await VerifyAuthentication();
        if (authenticated)
        {
            _logger.LogInformation("Authentication confirmed via API access");
            return true;
        }

        _logger.LogWarning("At MinUddannelse but API access failed - may need another redirect");
        return false;
    }

    private bool CheckForAuthenticationSuccess(HtmlDocument doc, string currentUrl, int stepCounter)
    {
        if (!currentUrl.Contains("/Node/") && !currentUrl.Contains("/portal/") &&
            !(currentUrl.Contains("minuddannelse.net") && !currentUrl.Contains("Login") && !currentUrl.Contains("Forside")))
        {
            return false;
        }

        _logger.LogDebug("Possible success - URL indicates we're back at MinUddannelse");
        _logger.LogDebug("Checking for user profile");

        var userInfo = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'user') or contains(@class, 'profile')]");
        if (userInfo != null)
        {
            _logger.LogDebug("Found user info element");
        }

        var scripts = doc.DocumentNode.SelectNodes("//script");
        if (scripts != null)
        {
            foreach (var script in scripts)
            {
                if (script.InnerText.Contains("currentUser") ||
                    script.InnerText.Contains("bruger") ||
                    script.InnerText.Contains("elev"))
                {
                    _logger.LogInformation("Found user context in script - authentication successful");
                    return true;
                }
            }
        }

        if (stepCounter > 5 && currentUrl.Contains("minuddannelse.net"))
        {
            _logger.LogInformation("After multiple redirects, back at MinUddannelse - assuming success");
            return true;
        }

        return false;
    }

    private async Task<(bool authenticated, bool hasSubmittedCredentials, string newContent, string newUrl)> TrySubmitForm(
        string content, string currentUrl, bool hasSubmittedCredentials)
    {
        var formData = ExtractFormData(content);
        if (formData == null)
        {
            return (false, hasSubmittedCredentials, content, currentUrl);
        }

        _logger.LogDebug("Submitting form to: {FormUrl}", formData.Item1);
        _logger.LogDebug("Form data fields: {FormFields}", string.Join(", ", formData.Item2.Keys));

        var isCredentialForm = formData.Item2.ContainsKey("username") ||
                              formData.Item2.ContainsKey("Username") ||
                              formData.Item2.ContainsKey("j_username");

        if (isCredentialForm)
        {
            _logger.LogDebug("Submitting credentials for user: {Username}", _username);
            hasSubmittedCredentials = true;
        }

        var response = await HttpClient.PostAsync(formData.Item1, new FormUrlEncodedContent(formData.Item2));
        var newContent = await response.Content.ReadAsStringAsync();
        var newUrl = response.RequestMessage?.RequestUri?.ToString() ?? currentUrl;

        _logger.LogDebug("Response status: {StatusCode}", response.StatusCode);
        _logger.LogDebug("New URL: {CurrentUrl}", newUrl);

        if (hasSubmittedCredentials && newUrl.Contains("minuddannelse.net"))
        {
            _logger.LogDebug("Returned to MinUddannelse after credentials, verifying");
            var authenticated = await VerifyAuthentication();
            if (authenticated)
            {
                _logger.LogInformation("Login successful");
                HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                return (true, hasSubmittedCredentials, newContent, newUrl);
            }
        }

        var success = CheckIfLoginSuccessful(response);
        if (success)
        {
            _logger.LogInformation("Login successful (URL match)");
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return (true, hasSubmittedCredentials, newContent, newUrl);
        }

        return (false, hasSubmittedCredentials, newContent, newUrl);
    }

    private async Task<(bool navigated, string newContent, string newUrl)> TryAlternativeNavigation(
        HtmlDocument doc, string currentUrl)
    {
        var uniLoginLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, 'unilogin-idp-prod')]");
        if (uniLoginLink == null)
        {
            return (false, string.Empty, currentUrl);
        }

        var href = uniLoginLink.Attributes["href"]?.Value;
        _logger.LogDebug("Found UniLogin link, navigating to: {Href}", href);

        if (!string.IsNullOrEmpty(href))
        {
            if (!href.StartsWith("http"))
            {
                var baseUri = new Uri(currentUrl);
                var absoluteUri = new Uri(baseUri, href);
                href = absoluteUri.ToString();
            }

            _logger.LogDebug("Following UniLogin link to: {Href}", href);
            var response = await HttpClient.GetAsync(href);
            var newContent = await response.Content.ReadAsStringAsync();
            var newUrl = response.RequestMessage?.RequestUri?.ToString() ?? href;

            _logger.LogDebug("After following link - Status: {StatusCode}", response.StatusCode);
            _logger.LogDebug("New URL: {CurrentUrl}", newUrl);

            return (true, newContent, newUrl);
        }

        return (false, string.Empty, currentUrl);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                HttpClient?.Dispose();
            }
            _disposed = true;
        }
    }
}
