using Newtonsoft.Json.Linq;
using Aula.Configuration;

namespace Aula.Services;

public interface IDataService
{
    void CacheWeekLetter(Child child, JObject weekLetter);
    JObject? GetWeekLetter(Child child);

    void CacheWeekSchedule(Child child, JObject weekSchedule);
    JObject? GetWeekSchedule(Child child);

    IEnumerable<Child> GetChildren();
}