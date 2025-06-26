using Newtonsoft.Json.Linq;

namespace Aula;

public interface IMinUddannelseClient
{
    Task<bool> LoginAsync();
    Task<JObject> GetWeekLetter(Child child, DateOnly date);
    Task<JObject> GetWeekSchedule(Child child, DateOnly date);
} 