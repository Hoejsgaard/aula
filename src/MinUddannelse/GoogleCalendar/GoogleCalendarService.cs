using System.Globalization;
using Microsoft.Extensions.Logging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MinUddannelse.Configuration;

namespace MinUddannelse.GoogleCalendar;

public class GoogleCalendarService : IGoogleCalendarService
{
    private readonly CalendarService _calendarService;
    private readonly string _prefix;
    private readonly ILogger _logger;


    public GoogleCalendarService(GoogleServiceAccount serviceAccount, string prefix, ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrEmpty(prefix)) throw new ArgumentNullException(nameof(prefix));
        if (prefix.Length < 3) throw new ArgumentException("Prefix must be at least 3 chars");
        _prefix = prefix;
        _logger = loggerFactory.CreateLogger<GoogleCalendarService>();
        var credential = GoogleCredential.FromJson(GetJsonKey(serviceAccount))
            .CreateScoped(CalendarService.Scope.Calendar);

        _calendarService = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "AulaBot"
        });

    }

    private string GetJsonKey(GoogleServiceAccount serviceAccount)
    {
        var credentialObject = new
        {
            type = serviceAccount.Type,
            project_id = serviceAccount.ProjectId,
            private_key_id = serviceAccount.PrivateKeyId,
            private_key = serviceAccount.PrivateKey,
            client_email = serviceAccount.ClientEmail,
            client_id = serviceAccount.ClientId,
            auth_uri = serviceAccount.AuthUri,
            token_uri = serviceAccount.TokenUri,
            auth_provider_x509_cert_url = serviceAccount.AuthProviderX509CertUrl,
            client_x509_cert_url = serviceAccount.ClientX509CertUrl
        };

        return JsonConvert.SerializeObject(credentialObject);
    }

    private async Task<Google.Apis.Calendar.v3.Data.Events> GetEventsForCurrentWeek(string calendarId)
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

    public async Task<IList<Event>> GetEventsThisWeek(string calendarId)
    {
        var events = await GetEventsForCurrentWeek(calendarId);

        return events.Items;
    }

    public async Task<bool> SynchronizeWeek(string googleCalendarId, DateOnly dateInWeek, JObject jsonEvents)
    {
        // Calculate start and end of the week
        var weekStart = DateOnly.FromDateTime(dateInWeek.ToDateTime(TimeOnly.MinValue)
            .AddDays(-(int)dateInWeek.DayOfWeek + (int)CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek));
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

            foreach (var eventItem in events.Items.Where(e => e.Summary?.StartsWith(prefix) == true))
                await _calendarService.Events.Delete(calendarId, eventItem.Id).ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear events for calendar {CalendarId} with prefix {Prefix}", calendarId, _prefix);
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
                    var summary = (_prefix.TrimEnd() + " " + jEvent["subject"]).Trim();
                    var location = jEvent["location"]?.ToString() ?? "";
                    var start = jEvent["timeBegin"]?.ToString();
                    var end = jEvent["timeEnd"]?.ToString();
                    if (start == null || end == null) throw new InvalidCalendarEventException("Events must have start and end");

                    var newEvent = new Event
                    {
                        Summary = summary,
                        Location = location,
                        Start = new EventDateTime
                        {
                            DateTimeDateTimeOffset = DateTimeOffset.Parse(start, CultureInfo.InvariantCulture)
                        },
                        End = new EventDateTime
                        {
                            DateTimeDateTimeOffset = DateTimeOffset.Parse(end, CultureInfo.InvariantCulture)
                        }
                    };

                    await _calendarService.Events.Insert(newEvent, calendarId).ExecuteAsync();
                }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create events for calendar {CalendarId} with prefix {Prefix}", calendarId, _prefix);
            return false;
        }

        return true;
    }
}
