using Aula.Configuration;
using Aula.Models;
using Aula.Repositories.DTOs;
using Newtonsoft.Json.Linq;

namespace Aula.MinUddannelse;

[Obsolete("This interface will be replaced with IWeekLetterService in future versions")]
public interface IMinUddannelseClient
{
    Task<JObject> GetWeekLetter(Child child, DateOnly date, bool allowLiveFetch = false);
    Task<JObject> GetWeekSchedule(Child child, DateOnly date);
}
