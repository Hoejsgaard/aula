using System.Globalization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Newtonsoft.Json.Linq;

namespace Aula;

public class GoogleCalendar
{
	private readonly CalendarService _calendarService;
	private readonly string _prefix;


	public GoogleCalendar(GoogleServiceAccount serviceAccount, string prefix)
	{
		if (string.IsNullOrEmpty(prefix)) throw new ArgumentNullException(nameof(prefix));
		if (prefix.Length < 3) throw new ArgumentException("Prefix must be at least 3 chars");
		_prefix = prefix;
		var credential = GoogleCredential.FromJson(GetJsonKey(serviceAccount))
			.CreateScoped(CalendarService.Scope.Calendar);

		_calendarService = new CalendarService(new BaseClientService.Initializer
		{
			HttpClientInitializer = credential,
			ApplicationName = "AulaBot"
		});

		// Your calendar management logic here
	}


	private string GetJsonKey(GoogleServiceAccount serviceAccount)
	{
		return $@"{{
	            ""type"": ""{serviceAccount.Type}"",
	            ""project_id"": ""{serviceAccount.ProjectId}"",
	            ""private_key_id"": ""{serviceAccount.PrivateKeyId}"",
	            ""private_key"": ""{serviceAccount.PrivateKey}"",
	            ""client_email"": ""{serviceAccount.ClientEmail}"",
	            ""client_id"": ""{serviceAccount.ClientId}"",
	            ""auth_uri"": ""{serviceAccount.AuthUri}"",
	            ""token_uri"": ""{serviceAccount.TokenUri}"",
	            ""auth_provider_x509_cert_url"": ""{serviceAccount.AuthProviderX509CertUrl}"",
	            ""client_x509_cert_url"": ""{serviceAccount.ClientX509CertUrl}"",
	        }}";
	}

	private async Task<Events> GetEventsForCurrentWeek(string calendarId)
	{
		// Calculate the start and end dates of the current week
		var currentDate = DateTime.UtcNow;
		var currentDayOfWeek = (int)currentDate.DayOfWeek;
		var difference = currentDayOfWeek - (int)CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek;
		var firstDayOfWeek = currentDate.AddDays(-difference).Date;
		var lastDayOfWeek = firstDayOfWeek.AddDays(7).AddTicks(-1); // End of Sunday

		var request = _calendarService.Events.List(calendarId);
		request.TimeMaxDateTimeOffset = lastDayOfWeek;
		request.TimeMinDateTimeOffset = firstDayOfWeek;
		request.ShowDeleted = false;
		request.SingleEvents = true;
		request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

		return await request.ExecuteAsync();
	}

	public async Task<IList<Event>> GetEventsThisWeeek(string calendarId)
	{
		var events = await GetEventsForCurrentWeek(calendarId);
		Console.WriteLine("Events this week:");
		foreach (var eventItem in events.Items)
		{
			var dateTimeOffset = eventItem.Start.DateTimeDateTimeOffset?.ToString() ?? "no date time offset";

			Console.WriteLine($"{eventItem.Summary} ({dateTimeOffset})");
		}

		return events.Items;
	}

	public async Task<bool> CreateEventTEST(string calendarId)
	{
		var newEvent = new Event
		{
			Summary = "TEST Testing event creation",
			Location = "TEST Bakkegårdsskolen",
			Start = new EventDateTime
			{
				DateTimeDateTimeOffset = new DateTime(2024, 1, 10, 15, 0, 0),
				TimeZone = "Europe/Copenhagen"
			},
			End = new EventDateTime
			{
				DateTimeDateTimeOffset = new DateTime(2024, 1, 10, 16, 0, 0),
				TimeZone = "Europe/Copenhagen"
			}
		};

		try
		{
			var request = _calendarService.Events.Insert(newEvent, calendarId);
			var createdEvent = await request.ExecuteAsync();
			Console.WriteLine("Event created: {0}", createdEvent.HtmlLink);

			return true;
		}
		catch
		{
			return false;
		}
	}


	public async Task<bool> SynchronizeWeek(string googleCalendarId, DateOnly dateInWeek, JObject jsonEvents)
	{
		// Calculate start and end of the week
		var weekStart = DateOnly.FromDateTime(dateInWeek.ToDateTime(TimeOnly.MinValue)
			.AddDays(-(int)dateInWeek.DayOfWeek + (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek));
		var weekEnd = weekStart.AddDays(7);

		if (await ClearEvents(googleCalendarId, weekStart, weekEnd, _prefix))
			return await CreateEventsFromJson(googleCalendarId, jsonEvents);

		return false;
	}

	private async Task<bool> ClearEvents(string calendarId, DateOnly weekStart, DateOnly weekEnd, string prefix)
	{
		try
		{
			var request = _calendarService.Events.List(calendarId);
			request.TimeMinDateTimeOffset = weekStart.ToDateTime(TimeOnly.MinValue);
			request.TimeMaxDateTimeOffset = weekEnd.ToDateTime(TimeOnly.MaxValue);
			request.SingleEvents = true;

			var events = await request.ExecuteAsync();

			foreach (var eventItem in events.Items.Where(e => e.Summary.StartsWith(prefix)))
				await _calendarService.Events.Delete(calendarId, eventItem.Id).ExecuteAsync();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> CreateEventsFromJson(string calendarId, JObject jsonEvents)
	{
		var schedule = jsonEvents["skema"];
		var events = schedule?["events"];

		try
		{
			if (events != null)
				foreach (var jEvent in events)
				{
					var summary = _prefix + " " +  jEvent["subject"];
					var location = jEvent["location"]?.ToString() ?? "";
					var start = jEvent["timeBegin"]?.ToString();
					var end = jEvent["timeEnd"]?.ToString();
					if (start == null || end == null) throw new Exception("Events must have start and end");

					var newEvent = new Event
					{
						Summary = summary,
						Location = location,
						Start = new EventDateTime
						{
							DateTimeDateTimeOffset = DateTimeOffset.Parse(start)
						},
						End = new EventDateTime
						{
							DateTimeDateTimeOffset = DateTimeOffset.Parse(end)
						},
						
					};

					await _calendarService.Events.Insert(newEvent, calendarId).ExecuteAsync();
				}
		}
		catch
		{
			return false;
		}

		return true;
	}
}