using System.Net;
using System.Net.Http.Headers;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Aula.Configuration;

namespace Aula.Integration;

public abstract class UniLoginClient
{
	private readonly string _loginUrl;
	private readonly string _password;
	private readonly string _username;
	private bool _loggedIn;

	private JObject _userProfile = new();

	public UniLoginClient(string username, string password, string loginUrl, string successUrl)
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
		var response = await HttpClient.GetAsync(_loginUrl);
		var content = await response.Content.ReadAsStringAsync();

		return await ProcessLoginResponseAsync(content);
	}


	private async Task<bool> ProcessLoginResponseAsync(string content)
	{
		var maxSteps = 10;
		var success = false;
		var hasSubmittedCredentials = false;

		for (var stepCounter = 0; stepCounter < maxSteps; stepCounter++)
		{
			try
			{
				var formData = ExtractFormData(content);

				// Check if this form contains credentials
				if (formData.Item2.ContainsKey("username") ||
					formData.Item2.ContainsKey("Username") ||
					formData.Item2.ContainsKey("j_username"))
				{
					hasSubmittedCredentials = true;
					Console.WriteLine($"[UniLogin] Submitting credentials at step {stepCounter}");
				}

				var response = await HttpClient.PostAsync(formData.Item1, new FormUrlEncodedContent(formData.Item2));
				content = await response.Content.ReadAsStringAsync();

				// Check if we're back at MinUddannelse after submitting credentials
				var currentUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";
				if (hasSubmittedCredentials && currentUrl.Contains("minuddannelse.net"))
				{
					Console.WriteLine($"[UniLogin] Back at MinUddannelse after credentials");

					// Verify authentication with API call
					var apiUrl = $"https://www.minuddannelse.net/api/stamdata/elev/getElev?_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
					var apiResponse = await HttpClient.GetAsync(apiUrl);

					if (apiResponse.IsSuccessStatusCode)
					{
						Console.WriteLine($"[UniLogin] API verification successful - authenticated!");
						HttpClient.DefaultRequestHeaders.Accept.Add(
							new MediaTypeWithQualityHeaderValue("application/json"));
						return true;
					}
				}

				success = CheckIfLoginSuccessful(response);
				if (success)
				{
					HttpClient.DefaultRequestHeaders.Accept.Add(
						new MediaTypeWithQualityHeaderValue("application/json"));
					return true;
				}
			}
			catch (Exception ex)
			{
				// Log the exception for debugging
				Console.WriteLine($"[UniLogin] Step {stepCounter} failed: {ex.Message}");

				// If we can't extract form data, we might be at the end of the flow
				if (ex.Message.Contains("Form not found") && hasSubmittedCredentials)
				{
					Console.WriteLine($"[UniLogin] No more forms after credentials - checking authentication");

					// Try API verification
					try
					{
						var apiUrl = $"https://www.minuddannelse.net/api/stamdata/elev/getElev?_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
						var apiResponse = await HttpClient.GetAsync(apiUrl);
						if (apiResponse.IsSuccessStatusCode)
						{
							Console.WriteLine($"[UniLogin] API verification successful!");
							HttpClient.DefaultRequestHeaders.Accept.Add(
								new MediaTypeWithQualityHeaderValue("application/json"));
							return true;
						}
					}
					catch { }
				}
			}
		}

		return success;
	}

	private Tuple<string, Dictionary<string, string>> ExtractFormData(string htmlContent)
	{
		var doc = new HtmlDocument();
		doc.LoadHtml(htmlContent);
		var formNode = doc.DocumentNode.SelectSingleNode("//form");

		if (formNode == null) throw new Exception("Form not found");

		var actionUrl = formNode.Attributes["action"]?.Value;
		if (actionUrl == null) throw new Exception("No action node found");
		var formData = BuildFormData(doc);
		var writer = new StringWriter();
		HttpUtility.HtmlDecode(actionUrl, writer);
		var decodedUrl = writer.ToString();
		return new Tuple<string, Dictionary<string, string>>(decodedUrl, formData);
	}

	private Dictionary<string, string> BuildFormData(HtmlDocument document)
	{
		var formData = new Dictionary<string, string>();

		var inputs = document.DocumentNode.SelectNodes("//input");
		if (inputs == null)
		{
			formData.Add("selectedIdp", "uni_idp");
			return formData;
		}

		foreach (var input in inputs)
		{
			var name = input.Attributes["name"]?.Value;
			var value = input.GetAttributeValue("value", string.Empty);

			if (string.IsNullOrWhiteSpace(name)) continue;

			// Handle various field names for username and password
			var lowerName = name.ToLowerInvariant();
			formData[name] = lowerName switch
			{
				"username" or "j_username" or "user" or "login" => _username,
				"password" or "j_password" or "pass" or "pwd" => _password,
				_ => value
			};
		}

		return formData;
	}

	private bool CheckIfLoginSuccessful(HttpResponseMessage response)
	{
		_loggedIn = response.RequestMessage?.RequestUri?.ToString() == SuccessUrl;

		return _loggedIn;
	}
}
