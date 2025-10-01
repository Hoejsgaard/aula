using System;
using Newtonsoft.Json.Linq;
using Aula.Configuration;

namespace Aula.Services;

[Obsolete("Use IWeekLetterService with IChildContext instead. This interface will be removed in the next major version. " +
          "Migrate to child-aware services using ChildServiceCoordinator for proper isolation.")]
public interface IDataService
{
    void CacheWeekLetter(Child child, int weekNumber, int year, JObject weekLetter);
    JObject? GetWeekLetter(Child child, int weekNumber, int year);

    void CacheWeekSchedule(Child child, int weekNumber, int year, JObject weekSchedule);
    JObject? GetWeekSchedule(Child child, int weekNumber, int year);

    IEnumerable<Child> GetChildren();
}
