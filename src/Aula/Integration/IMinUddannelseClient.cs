using Newtonsoft.Json.Linq;
using Aula.Configuration;

namespace Aula.Integration;

public interface IMinUddannelseClient
{
    Task<bool> LoginAsync();
    Task<JObject> GetWeekLetter(Child child, DateOnly date);
    Task<JObject> GetWeekSchedule(Child child, DateOnly date);
}