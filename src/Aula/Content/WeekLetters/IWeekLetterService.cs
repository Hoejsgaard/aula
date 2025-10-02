using Aula.Configuration;
using Aula.Content.WeekLetters;
using Aula.Core.Models;
using Newtonsoft.Json.Linq;

namespace Aula.Content.WeekLetters;

public interface IWeekLetterService
{
    Task CacheWeekLetterAsync(Child child, int weekNumber, int year, JObject weekLetter);
    Task<JObject?> GetWeekLetterAsync(Child child, int weekNumber, int year);
    Task CacheWeekScheduleAsync(Child child, int weekNumber, int year, JObject weekSchedule);
    Task<JObject?> GetWeekScheduleAsync(Child child, int weekNumber, int year);
    Task<bool> StoreWeekLetterAsync(Child child, int weekNumber, int year, JObject weekLetter);
    Task<bool> DeleteWeekLetterAsync(Child child, int weekNumber, int year);
    Task<List<JObject>> GetStoredWeekLettersAsync(Child child, int? year = null);
    Task<JObject?> GetOrFetchWeekLetterAsync(Child child, DateOnly date, bool allowLiveFetch = false);
    Task<List<JObject>> GetAllWeekLettersAsync(Child child);
}
