using System.Net;
using System.Runtime.InteropServices.Marshalling;
using System.Web;
using HtmlAgilityPack;

namespace Aula;

public class AulaClient
{
	private const string _aulaApi = "https://www.aula.dk/api/v17/";
	private readonly HttpClient _httpClient;
	private readonly HttpClientHandler _httpClientHandler;
	private readonly string _password;
	private readonly string _username;
	private bool _loggedIn;

	public AulaClient(string username, string password)
	{
		_httpClientHandler = new HttpClientHandler
		{
			CookieContainer = new CookieContainer(),
			UseCookies = true,
			AllowAutoRedirect = true
		};
		_httpClient = new HttpClient(_httpClientHandler);
		_username = username ?? throw new ArgumentNullException(nameof(username));
		_password = password ?? throw new ArgumentNullException(nameof(password));
	}

	public async Task<HttpResponseMessage> GetProfile()
	{
		return await _httpClient.GetAsync(_aulaApi + "?method=profiles.getProfilesByLogin");
	}

	public async Task<HttpResponseMessage> GetProfileContext()
	{
		return await _httpClient.GetAsync(_aulaApi + "?method=profiles.getProfileContext");
	}

	public async Task<bool> LoginAsync()
	{
		var loginUrl = "https://www.aula.dk/auth/login.php?type=unilogin";
		var response = await _httpClient.GetAsync(loginUrl);
		var content = await response.Content.ReadAsStringAsync();

		return await ProcessLoginResponseAsync(content);
	}

	private async Task<bool> ProcessLoginResponseAsync(string content)
	{
		int maxSteps = 10;
		var success = false;
		for (var stepCounter = 0; stepCounter < maxSteps; stepCounter++)
			try
			{
				var formData = ExtractFormData(content);
				var response = await _httpClient.PostAsync(formData.Item1, new FormUrlEncodedContent(formData.Item2));
				content = await response.Content.ReadAsStringAsync();

				success = CheckIfLoginSuccessful(response);
				if (success) return true;
			}
			catch (Exception)
			{
				// ignored - this is so fragile I can't be bothered
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

			formData[name] = name switch
			{
				"username" => _username,
				"password" => _password,
				_ => value
			};
		}

		return formData;
	}

	private bool CheckIfLoginSuccessful(HttpResponseMessage response)
	{
		_loggedIn = response.RequestMessage?.RequestUri?.ToString() == "https://www.aula.dk/portal/";

		return _loggedIn;
	}
}