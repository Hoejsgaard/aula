using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aula.Services;

public interface IScheduledTaskRepository
{
    Task<List<ScheduledTask>> GetScheduledTasksAsync();
    Task<ScheduledTask?> GetScheduledTaskAsync(string name);
    Task UpdateScheduledTaskAsync(ScheduledTask task);
}