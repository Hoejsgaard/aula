using System.Net;
using System.Net.Http.Headers;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Aula.Configuration;

namespace Aula.Integration;

/// <summary>
/// Debug version of UniLoginClient that logs detailed information about each step
/// </summary>
public abstract class UniLoginDebugClient
{
    private readonly string _loginUrl;
    private readonly string _password;
    private readonly string _username;
    private bool _loggedIn;

    private JObject _userProfile = new();

    public UniLoginDebugClient(string username, string password, string loginUrl, string successUrl)
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
    }

    protected HttpClient HttpClient { get; }
    protected string SuccessUrl { get; }

    public async Task<bool> LoginAsync()
    {
        Console.WriteLine($"[DEBUG] Starting login process for user: {_username}");
        Console.WriteLine($"[DEBUG] Initial URL: {_loginUrl}");

        var response = await HttpClient.GetAsync(_loginUrl);
        var content = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"[DEBUG] Initial response status: {response.StatusCode}");
        Console.WriteLine($"[DEBUG] Response URL: {response.RequestMessage?.RequestUri}");

        return await ProcessLoginResponseAsync(content, response);
    }

    private async Task<bool> ProcessLoginResponseAsync(string content, HttpResponseMessage initialResponse)
    {
        var maxSteps = 10;
        var success = false;
        var currentUrl = initialResponse.RequestMessage?.RequestUri?.ToString() ?? _loginUrl;
        var hasSubmittedCredentials = false;

        for (var stepCounter = 0; stepCounter < maxSteps; stepCounter++)
        {
            Console.WriteLine($"\n[DEBUG] ===== STEP {stepCounter} =====");
            Console.WriteLine($"[DEBUG] Current URL: {currentUrl}");
            Console.WriteLine($"[DEBUG] Content length: {content.Length} chars");

            // Track if we've submitted credentials
            if (currentUrl.Contains("broker.unilogin.dk") && content.Contains("password"))
            {
                hasSubmittedCredentials = true;
                Console.WriteLine($"[DEBUG] 🔑 Credentials form detected");
            }

            // After submitting credentials and returning to MinUddannelse, we should be authenticated
            if (hasSubmittedCredentials && currentUrl.Contains("minuddannelse.net") && !currentUrl.Contains("Login"))
            {
                Console.WriteLine($"[DEBUG] ✅ Back at MinUddannelse after credential submission");

                // Verify authentication by testing multiple endpoints
                var authenticated = await VerifyAuthentication();
                if (authenticated)
                {
                    Console.WriteLine($"[DEBUG] ✅ Authentication confirmed via API access!");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[DEBUG] ⚠️ At MinUddannelse but API access failed - may need another redirect");
                }
            }

            try
            {
                // Parse the HTML
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                // Log page title
                var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
                Console.WriteLine($"[DEBUG] Page title: {title ?? "No title found"}");

                // Look for different types of forms
                var forms = doc.DocumentNode.SelectNodes("//form");
                if (forms != null)
                {
                    Console.WriteLine($"[DEBUG] Found {forms.Count} form(s) on page");
                    foreach (var form in forms)
                    {
                        var formAction = form.Attributes["action"]?.Value;
                        var formMethod = form.Attributes["method"]?.Value;
                        var formId = form.Attributes["id"]?.Value;
                        var formName = form.Attributes["name"]?.Value;
                        Console.WriteLine($"[DEBUG]   Form: action='{formAction}', method='{formMethod}', id='{formId}', name='{formName}'");

                        // Log input fields in the form
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
                                    Console.WriteLine($"[DEBUG]	 Input: name='{inputName}', type='{inputType}', id='{inputId}'");
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] No forms found on this page");

                    // Look for JavaScript redirects or other navigation elements
                    var scripts = doc.DocumentNode.SelectNodes("//script");
                    if (scripts != null)
                    {
                        foreach (var script in scripts)
                        {
                            if (script.InnerText.Contains("window.location") ||
                                script.InnerText.Contains("redirect") ||
                                script.InnerText.Contains("submit"))
                            {
                                Console.WriteLine($"[DEBUG] Found potential JavaScript redirect/submit");
                                // Log first 200 chars of the script
                                var scriptPreview = script.InnerText.Length > 200
                                    ? script.InnerText.Substring(0, 200) + "..."
                                    : script.InnerText;
                                Console.WriteLine($"[DEBUG] Script preview: {scriptPreview}");
                            }
                        }
                    }

                    // Look for links that might be login-related
                    var links = doc.DocumentNode.SelectNodes("//a[contains(@href, 'login') or contains(@href, 'Login') or contains(@href, 'auth')]");
                    if (links != null)
                    {
                        Console.WriteLine($"[DEBUG] Found {links.Count} login-related link(s)");
                        foreach (var link in links)
                        {
                            var href = link.Attributes["href"]?.Value;
                            var text = link.InnerText?.Trim();
                            Console.WriteLine($"[DEBUG]   Link: href='{href}', text='{text}'");
                        }
                    }

                    // Check for meta refresh
                    var metaRefresh = doc.DocumentNode.SelectSingleNode("//meta[@http-equiv='refresh']");
                    if (metaRefresh != null)
                    {
                        var refreshContent = metaRefresh.Attributes["content"]?.Value;
                        Console.WriteLine($"[DEBUG] Meta refresh found: {refreshContent}");
                    }
                }

                // Check if we're at various success pages
                if (currentUrl.Contains("/Node/") ||
                    currentUrl.Contains("/portal/") ||
                    currentUrl.Contains("minuddannelse.net") && !currentUrl.Contains("Login") && !currentUrl.Contains("Forside"))
                {
                    Console.WriteLine($"[DEBUG] Possible success - URL indicates we're back at MinUddannelse");
                    Console.WriteLine($"[DEBUG] Checking for user profile...");

                    // Look for user info on the page
                    var userInfo = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'user') or contains(@class, 'profile')]");
                    if (userInfo != null)
                    {
                        Console.WriteLine($"[DEBUG] Found user info element");
                    }

                    // Check if we have user context in scripts
                    var scripts = doc.DocumentNode.SelectNodes("//script");
                    if (scripts != null)
                    {
                        foreach (var script in scripts)
                        {
                            if (script.InnerText.Contains("currentUser") ||
                                script.InnerText.Contains("bruger") ||
                                script.InnerText.Contains("elev"))
                            {
                                Console.WriteLine($"[DEBUG] Found user context in script - authentication successful!");
                                return true;
                            }
                        }
                    }

                    // If we're at MinUddannelse after all those redirects, we're likely authenticated
                    if (stepCounter > 5 && currentUrl.Contains("minuddannelse.net"))
                    {
                        Console.WriteLine($"[DEBUG] After multiple redirects, back at MinUddannelse - assuming success");
                        return true;
                    }
                }

                // Try to extract and submit form if found
                var formData = ExtractFormData(content);
                if (formData != null)
                {
                    Console.WriteLine($"[DEBUG] Submitting form to: {formData.Item1}");
                    Console.WriteLine($"[DEBUG] Form data fields: {string.Join(", ", formData.Item2.Keys)}");

                    // Check if this is a credential submission form
                    var isCredentialForm = formData.Item2.ContainsKey("username") ||
                                          formData.Item2.ContainsKey("Username") ||
                                          formData.Item2.ContainsKey("j_username");

                    if (isCredentialForm)
                    {
                        Console.WriteLine($"[DEBUG] 🔐 Submitting credentials for user: {_username}");
                        hasSubmittedCredentials = true;
                    }

                    var response = await HttpClient.PostAsync(formData.Item1, new FormUrlEncodedContent(formData.Item2));
                    content = await response.Content.ReadAsStringAsync();
                    currentUrl = response.RequestMessage?.RequestUri?.ToString() ?? currentUrl;

                    Console.WriteLine($"[DEBUG] Response status: {response.StatusCode}");
                    Console.WriteLine($"[DEBUG] New URL: {currentUrl}");

                    // After form submission, check if we're authenticated
                    if (hasSubmittedCredentials && currentUrl.Contains("minuddannelse.net"))
                    {
                        Console.WriteLine($"[DEBUG] Returned to MinUddannelse after credentials, verifying...");
                        var authenticated = await VerifyAuthentication();
                        if (authenticated)
                        {
                            Console.WriteLine($"[DEBUG] ✅ Login successful!");
                            HttpClient.DefaultRequestHeaders.Accept.Add(
                                new MediaTypeWithQualityHeaderValue("application/json"));
                            return true;
                        }
                    }

                    success = CheckIfLoginSuccessful(response);
                    if (success)
                    {
                        Console.WriteLine($"[DEBUG] ✅ Login successful (URL match)!");
                        HttpClient.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/json"));
                        return true;
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Could not extract form data from current page");

                    // Check if we need to click a login link
                    var uniLoginLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, 'unilogin-idp-prod')]");
                    if (uniLoginLink != null)
                    {
                        var href = uniLoginLink.Attributes["href"]?.Value;
                        Console.WriteLine($"[DEBUG] Found UniLogin link, navigating to: {href}");

                        // Make the URL absolute if needed
                        if (!string.IsNullOrEmpty(href))
                        {
                            if (!href.StartsWith("http"))
                            {
                                var baseUri = new Uri(currentUrl);
                                var absoluteUri = new Uri(baseUri, href);
                                href = absoluteUri.ToString();
                            }

                            Console.WriteLine($"[DEBUG] Following UniLogin link to: {href}");
                            var response = await HttpClient.GetAsync(href);
                            content = await response.Content.ReadAsStringAsync();
                            currentUrl = response.RequestMessage?.RequestUri?.ToString() ?? href;

                            Console.WriteLine($"[DEBUG] After following link - Status: {response.StatusCode}");
                            Console.WriteLine($"[DEBUG] New URL: {currentUrl}");

                            // Continue to next iteration with new content
                            continue;
                        }
                    }

                    // If no form, check if we need to follow a redirect or click something
                    var loginButton = doc.DocumentNode.SelectSingleNode("//button[contains(text(), 'Log') or contains(text(), 'Uni')]");
                    if (loginButton != null)
                    {
                        Console.WriteLine($"[DEBUG] Found login button: {loginButton.InnerText}");
                    }

                    // Check if page has any error messages
                    var errorElements = doc.DocumentNode.SelectNodes("//*[contains(@class, 'error') or contains(@class, 'alert')]");
                    if (errorElements != null)
                    {
                        foreach (var error in errorElements)
                        {
                            Console.WriteLine($"[DEBUG] Possible error message: {error.InnerText?.Trim()}");
                        }
                    }

                    break; // Can't proceed without a form or link
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Step {stepCounter} failed with exception: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
            }
        }

        Console.WriteLine($"[DEBUG] Login failed after {maxSteps} steps");
        return success;
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
                Console.WriteLine("[DEBUG] No form found for data extraction");
                return null;
            }

            var actionUrl = formNode.Attributes["action"]?.Value;
            if (actionUrl == null)
            {
                Console.WriteLine("[DEBUG] Form has no action attribute");
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
            Console.WriteLine($"[DEBUG] ExtractFormData exception: {ex.Message}");
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
            Console.WriteLine("[DEBUG] No inputs found in form");
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
                Console.WriteLine($"[DEBUG] Adding to form: {name} = {(name.Contains("password", StringComparison.OrdinalIgnoreCase) ? "***" : value)}");
            }

            // Handle various field names for username and password
            var lowerName = name.ToLowerInvariant();
            if (lowerName == "username" || lowerName == "j_username" || lowerName == "user" || lowerName == "login")
            {
                formData[name] = _username;
                Console.WriteLine($"[DEBUG]   Setting username field '{name}' to: {_username}");
            }
            else if (lowerName == "password" || lowerName == "j_password" || lowerName == "pass" || lowerName == "pwd")
            {
                formData[name] = _password;
                Console.WriteLine($"[DEBUG]   Setting password field '{name}' to: ***");
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
                        Console.WriteLine($"[DEBUG] Adding select to form: {name} = {value}");
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
            Console.WriteLine($"[DEBUG] Login check: SUCCESS - URL matches success pattern");
        }
        else
        {
            Console.WriteLine($"[DEBUG] Login check: Current URL doesn't match success URL");
        }

        return _loggedIn;
    }

    private async Task<bool> VerifyAuthentication()
    {
        Console.WriteLine($"[DEBUG] Verifying authentication status...");

        // Try multiple endpoints to verify authentication
        var endpoints = new[]
        {
            "https://www.minuddannelse.net/api/stamdata/elev/getElev",
            "https://www.minuddannelse.net/api/stamdata/getProfiles",
            "https://www.minuddannelse.net/api/stamdata/bruger/getBruger"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var url = endpoint + "?_=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Console.WriteLine($"[DEBUG] Testing endpoint: {endpoint}");

                var response = await HttpClient.GetAsync(url);
                Console.WriteLine($"[DEBUG]   Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG]   Content length: {content.Length} chars");

                    // Check if we got actual JSON data (not error page)
                    if (content.StartsWith('{') || content.StartsWith('['))
                    {
                        Console.WriteLine($"[DEBUG]   ✅ Valid JSON response received");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]   Error: {ex.Message}");
            }
        }

        Console.WriteLine($"[DEBUG] ❌ All authentication verification endpoints failed");
        return false;
    }
}
