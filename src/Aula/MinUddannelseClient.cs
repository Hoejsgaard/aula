using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Aula;

public class MinUddannelseClient
{
	private readonly HttpClient _httpClient;
	private readonly string _password;
	private readonly string _username;
	private bool _loggedIn;
	private JObject _userProfile = new();

	public MinUddannelseClient(string username, string password)
	{
		var httpClientHandler = new HttpClientHandler
		{
			CookieContainer = new CookieContainer(),
			UseCookies = true,
			AllowAutoRedirect = true
		};
		_httpClient = new HttpClient(httpClientHandler);
		_username = username ?? throw new ArgumentNullException(nameof(username));
		_password = password ?? throw new ArgumentNullException(nameof(password));
	}

	public async Task<JObject> GetWeekLetter(Child? find)
	{
		// hardcoded URL just to test that it works
		var url = "https://www.minuddannelse.net/api/stamdata/ugeplan/getUgeBreve?tidspunkt=2024-W2&elevId=2643430&_=" +
		          DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var response = await _httpClient.GetAsync(url);
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync();
		return JObject.Parse(json);
	}

	public async Task<JObject> GetWeekLetter(Child child, DateOnly date)
	{
		var url = string.Format("https://www.minuddannelse.net/api/stamdata/ugeplan/getUgeBreve?tidspunkt={0}-W{1}&elevId={2}&_={3}"
			, date.Year, GetIsoWeekNumber(date), GetChildId(child), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		var response = await _httpClient.GetAsync(url);
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync();
		return JObject.Parse(json);
	}

	private string? GetChildId(Child child)
	{
		if (_userProfile == null) throw new Exception("User profile not loaded");
		//		var content = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
		var kids = _userProfile["boern"];
		if (kids == null) throw new Exception("No children found in user profile");
		string id = "";
		foreach (var kid in kids)
		{
			if (kid["fornavn"]?.ToString() == child.FirstName)
			{
				id = kid["id"]?.ToString() ?? "";
			}
		}

		if (id == "") throw new Exception("Child not found");
		
		return id;

	}

	private int GetIsoWeekNumber(DateOnly date)
	{
		var cultureInfo = CultureInfo.CurrentCulture;
		var calendarWeekRule = cultureInfo.DateTimeFormat.CalendarWeekRule;
		var firstDayOfWeek = cultureInfo.DateTimeFormat.FirstDayOfWeek;
		return cultureInfo.Calendar.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue), calendarWeekRule, firstDayOfWeek);
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
					_userProfile = ExtractUserProfile(content);

					//Console.WriteLine("MinUddannelse Profile");
					//Console.WriteLine(Program.PrettifyJson(_userProfile.ToString()));

					return true;
				}
			}
			catch (Exception)
			{
				// ignored - this is kind of fragile
			}

		return success;
	}

	/// <summary>
	///  Read profile data out of the front page. I can't find the api call, if it exist, that give me this data
	/// </summary>
	private JObject ExtractUserProfile(string html)
	{
		var doc = new HtmlDocument();
		doc.LoadHtml(html);

		// Find the script node that contains __tempcontext__
		var script = doc.DocumentNode.Descendants("script")
			.FirstOrDefault(n => n.InnerText.Contains("__tempcontext__"));

		if (script == null)
			throw new Exception("No UserProfile found");

		var scriptText = script.InnerText;
		var startIndex = scriptText.IndexOf("window.__tempcontext__['currentUser'] = ") +
		                 "window.__tempcontext__['currentUser'] = ".Length;
		var endIndex = scriptText.IndexOf(";", startIndex);

		var jsonText = scriptText.Substring(startIndex, endIndex - startIndex).Trim();

		return JObject.Parse(jsonText);
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