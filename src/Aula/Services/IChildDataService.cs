using Newtonsoft.Json.Linq;

namespace Aula.Services;

/// <summary>
/// Child-aware data service interface that uses IChildContext to determine the current child.
/// Provides secure, isolated data operations for each child without requiring Child parameters.
/// </summary>
public interface IChildDataService
{
	/// <summary>
	/// Caches a week letter for the current child from context.
	/// </summary>
	Task CacheWeekLetterAsync(int weekNumber, int year, JObject weekLetter);

	/// <summary>
	/// Retrieves a cached week letter for the current child from context.
	/// </summary>
	Task<JObject?> GetWeekLetterAsync(int weekNumber, int year);

	/// <summary>
	/// Caches a week schedule for the current child from context.
	/// </summary>
	Task CacheWeekScheduleAsync(int weekNumber, int year, JObject weekSchedule);

	/// <summary>
	/// Retrieves a cached week schedule for the current child from context.
	/// </summary>
	Task<JObject?> GetWeekScheduleAsync(int weekNumber, int year);

	/// <summary>
	/// Stores a week letter in the database for the current child.
	/// </summary>
	Task<bool> StoreWeekLetterAsync(int weekNumber, int year, JObject weekLetter);

	/// <summary>
	/// Deletes a week letter from the database for the current child.
	/// </summary>
	Task<bool> DeleteWeekLetterAsync(int weekNumber, int year);

	/// <summary>
	/// Retrieves stored week letters from the database for the current child.
	/// </summary>
	Task<List<JObject>> GetStoredWeekLettersAsync(int? year = null);

	/// <summary>
	/// Gets or fetches a week letter for the specified date.
	/// Will check cache first, then database, then fetch from MinUddannelse if allowed.
	/// </summary>
	Task<JObject?> GetOrFetchWeekLetterAsync(DateOnly date, bool allowLiveFetch = false);

	/// <summary>
	/// Gets all available week letters for the current child.
	/// </summary>
	Task<List<JObject>> GetAllWeekLettersAsync();
}
