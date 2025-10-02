using Aula.Models;
using Aula.Repositories.DTOs;
using Aula.Models;
using Aula.Repositories.DTOs;
using Aula.Models;
using Aula.Repositories.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aula.Repositories;

public interface IScheduledTaskRepository
{
    Task<List<ScheduledTask>> GetScheduledTasksAsync();
    Task<ScheduledTask?> GetScheduledTaskAsync(string name);
    Task UpdateScheduledTaskAsync(ScheduledTask task);
}
