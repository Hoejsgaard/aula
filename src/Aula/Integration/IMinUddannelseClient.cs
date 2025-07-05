using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Services;

namespace Aula.Integration;

public interface IMinUddannelseClient
{
    Task<bool> LoginAsync();
    Task<JObject> GetWeekLetter(Child child, DateOnly date);
    Task<JObject> GetWeekSchedule(Child child, DateOnly date);

    // Week letter storage and retrieval methods
    Task<JObject?> GetStoredWeekLetter(Child child, int weekNumber, int year);
    Task<JObject> GetWeekLetterWithFallback(Child child, DateOnly date);
    Task<List<StoredWeekLetter>> GetStoredWeekLetters(Child? child = null, int? year = null);
}