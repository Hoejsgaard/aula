using System.Net;
using System.Net.Http.Headers;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Aula;

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
		for (var stepCounter = 0; stepCounter < maxSteps; stepCounter++)
			try
			{
				var formData = ExtractFormData(content);
				var response = await HttpClient.PostAsync(formData.Item1, new FormUrlEncodedContent(formData.Item2));
				content = await response.Content.ReadAsStringAsync();


				success = CheckIfLoginSuccessful(response);
				if (success)
				{
					HttpClient.DefaultRequestHeaders.Accept.Add(
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
		_loggedIn = response.RequestMessage?.RequestUri?.ToString() == SuccessUrl;

		return _loggedIn;
	}
}