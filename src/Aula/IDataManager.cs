using Newtonsoft.Json.Linq;
using Aula.Configuration;

namespace Aula;

public interface IDataManager
{
    void CacheWeekLetter(Child child, JObject weekLetter);
    JObject? GetWeekLetter(Child child);

    void CacheWeekSchedule(Child child, JObject weekSchedule);
    JObject? GetWeekSchedule(Child child);

    IEnumerable<Child> GetChildren();
}