using System.Globalization;
using System.Net.Http.Headers;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Aula;

public class MinUddannelseClient : UniLoginClient
{
	private JObject _userProfile = new();

	public MinUddannelseClient(Config config) : this(config.UniLogin.Username, config.UniLogin.Password)
	{
		
	}

	public MinUddannelseClient(string username, string password) : base(username, password,
		"https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False",
		"https://www.minuddannelse.net/Node/")
	{
	}

	public async Task<JObject> GetWeekLetter(Child child, DateOnly date)
	{
		var url = string.Format(
			"https://www.minuddannelse.net/api/stamdata/ugeplan/getUgeBreve?tidspunkt={0}-W{1}&elevId={2}&_={3}"
			, date.Year, GetIsoWeekNumber(date), GetChildId(child), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		var response = await HttpClient.GetAsync(url);
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync();

		var weekLetter = JObject.Parse(json);
		var weekLettterArray = weekLetter["ugebreve"] as JArray;

		if (weekLettterArray == null || !weekLettterArray.Any())
		{
			var nullObject = new JObject
			{
				["klasseNavn"] = "N/A",
				["uge"] = $"{GetIsoWeekNumber(date)}",
				["indhold"] = "Der er ikke skrevet nogen ugenoter til denne uge",
			};
			weekLetter["ugebreve"] = new JArray(nullObject);

		}
		return weekLetter;
	}

	public async Task<JObject> GetWeekSchedule(Child child, DateOnly date)
	{
		var url = string.Format(
			"https://www.minuddannelse.net/api/stamdata/aulaskema/getElevSkema?elevId={0}&tidspunkt={1}-W{2}&_={3}",
			GetChildId(child), date.Year, GetIsoWeekNumber(date), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		var response = await HttpClient.GetAsync(url);
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync();
		return JObject.Parse(json);
	}


	private string? GetChildId(Child child)
	{
		if (_userProfile == null) throw new Exception("User profile not loaded");
		var kids = _userProfile["boern"];
		if (kids == null) throw new Exception("No children found in user profile");
		var id = "";
		foreach (var kid in kids)
			if (kid["fornavn"]?.ToString() == child.FirstName)
				id = kid["id"]?.ToString() ?? "";

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

	public new async Task<bool> LoginAsync()
	{
		var login = await base.LoginAsync();
		if (login)
		{
			HttpClient.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/json"));
			_userProfile = await ExtractUserProfile();
			return true;
		}

		return login;
	}

	/// <summary>
	///     Read profile data out of the front page. I can't find the api call, if it exist, that give me this data
	/// </summary>
	private async Task<JObject> ExtractUserProfile()
	{
		var response = await HttpClient.GetAsync(SuccessUrl);
		var content = await response.Content.ReadAsStringAsync();
		var doc = new HtmlDocument();
		doc.LoadHtml(content);

		// Find the script node that contains __tempcontext__5d
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
}