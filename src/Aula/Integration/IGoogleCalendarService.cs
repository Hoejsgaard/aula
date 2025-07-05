using Google.Apis.Calendar.v3.Data;
using Newtonsoft.Json.Linq;

namespace Aula.Integration;

public interface IGoogleCalendarService
{
    Task<IList<Event>> GetEventsThisWeek(string calendarId);
    Task<bool> SynchronizeWeek(string googleCalendarId, DateOnly dateInWeek, JObject jsonEvents);
}