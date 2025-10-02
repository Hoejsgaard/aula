using Aula.Models;
using Aula.Repositories.DTOs;
using Aula.Models;
using Aula.Repositories.DTOs;
using Aula.Models;
using Aula.Repositories.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aula.Repositories;

public interface IReminderRepository
{
    Task<int> AddReminderAsync(string text, DateOnly date, TimeOnly time, string? childName = null);
    Task<List<Reminder>> GetPendingRemindersAsync();
    Task MarkReminderAsSentAsync(int reminderId);
    Task<List<Reminder>> GetAllRemindersAsync();
    Task DeleteReminderAsync(int reminderId);
}
