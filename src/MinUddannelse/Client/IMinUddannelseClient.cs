using MinUddannelse.Configuration;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using Newtonsoft.Json.Linq;

namespace MinUddannelse.Client;

[Obsolete("This interface will be replaced with IWeekLetterService in future versions")]
public interface IMinUddannelseClient
{
    Task<JObject> GetWeekLetter(Child child, DateOnly date, bool allowLiveFetch = false);
    Task<JObject> GetWeekSchedule(Child child, DateOnly date);
}
