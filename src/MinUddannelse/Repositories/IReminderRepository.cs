using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MinUddannelse.Repositories;

public interface IReminderRepository
{
    Task<int> AddReminderAsync(string text, DateOnly date, TimeOnly time, string? childName = null);
    Task<List<Reminder>> GetPendingRemindersAsync();
    Task MarkReminderAsSentAsync(int reminderId);
    Task<List<Reminder>> GetAllRemindersAsync();
    Task DeleteReminderAsync(int reminderId);
}
