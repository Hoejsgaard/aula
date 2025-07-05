using Aula.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aula.Repositories;

public interface IScheduledTaskRepository
{
    Task<List<ScheduledTask>> GetScheduledTasksAsync();
    Task<ScheduledTask?> GetScheduledTaskAsync(string name);
    Task UpdateScheduledTaskAsync(ScheduledTask task);
}