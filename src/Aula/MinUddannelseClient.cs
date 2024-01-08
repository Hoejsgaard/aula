using System.Net;
using System.Net.Http.Headers;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Aula;

public class MinUddannelseClient
{
	private readonly HttpClient _httpClient;
	private readonly HttpClientHandler _httpClientHandler;
	private readonly string _password;
	private readonly string _username;
	private bool _loggedIn;

	public MinUddannelseClient(string username, string password)
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

	public async Task<JObject> GetWeekLetter()
	{
		// hardcoded URL just to test that it works
		var url = "https://www.minuddannelse.net/api/stamdata/ugeplan/getUgeBreve?tidspunkt=2024-W2&elevId=2643430&_=" +
		          DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var response = await _httpClient.GetAsync(url);
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync();
		return  JObject.Parse(json);
	}

	public async Task<bool> LoginAsync()
	{
		var loginUrl = "https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False";
		var response = await _httpClient.GetAsync(loginUrl);
		var content = await response.Content.ReadAsStringAsync();

		return await ProcessLoginResponseAsync(content);
	}


	private async Task<bool> ProcessLoginResponseAsync(string content)
	{
		var maxSteps = 10;
		var success = false;
		for (var stepCounter = 0; stepCounter < maxSteps; stepCounter++)
			try
			{
				var formData = ExtractFormData(content);
				var response = await _httpClient.PostAsync(formData.Item1, new FormUrlEncodedContent(formData.Item2));
				content = await response.Content.ReadAsStringAsync();

				success = CheckIfLoginSuccessful(response);
				if (success)
				{
					_httpClient.DefaultRequestHeaders.Accept.Add(
						new MediaTypeWithQualityHeaderValue("application/json"));
					return true;
				}
			}
			catch (Exception)
			{
				// ignored - this is kind of fragile
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
		_loggedIn = response.RequestMessage?.RequestUri?.ToString() == "https://www.minuddannelse.net/Node/";

		return _loggedIn;
	}
}