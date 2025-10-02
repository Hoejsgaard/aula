using System;
using Aula.Core.Models;
using Aula.External.MinUddannelse;
using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Services;

namespace Aula.External.MinUddannelse;

[Obsolete("Use IChildAuthenticationService with IChildContext instead. This interface will be removed in the next major version. " +
          "Authentication and data fetching should be done through child-aware services for proper isolation.")]
public interface IMinUddannelseClient
{
    Task<bool> LoginAsync();
    Task<JObject> GetWeekLetter(Child child, DateOnly date, bool allowLiveFetch = false);
    Task<JObject> GetWeekSchedule(Child child, DateOnly date);

    // Week letter storage and retrieval methods
    Task<JObject?> GetStoredWeekLetter(Child child, int weekNumber, int year);
    Task<List<StoredWeekLetter>> GetStoredWeekLetters(Child? child = null, int? year = null);
}
