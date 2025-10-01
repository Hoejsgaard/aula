using Newtonsoft.Json.Linq;

namespace Aula.Integration;

/// <summary>
/// Interface for child-authenticated clients that can fetch week letters and schedules
/// </summary>
public interface IChildAuthenticatedClient
{
    Task<bool> LoginAsync();
    Task<JObject> GetWeekLetter(DateOnly date);
    Task<JObject> GetWeekSchedule(DateOnly date);
}
