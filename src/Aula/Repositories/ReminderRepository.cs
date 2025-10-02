using Aula.Core.Models;
using Aula.Core.Models;
using Aula.Core.Models;
using Microsoft.Extensions.Logging;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aula.Repositories;

public class ReminderRepository : IReminderRepository
{
    private readonly Client _supabase;
    private readonly ILogger _logger;

    public ReminderRepository(Client supabase, ILoggerFactory loggerFactory)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        _logger = loggerFactory.CreateLogger<ReminderRepository>();
    }

    public async Task<int> AddReminderAsync(string text, DateOnly date, TimeOnly time, string? childName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!string.IsNullOrEmpty(childName))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        }

        var reminder = new Reminder
        {
            Text = text,
            RemindDate = date,
            RemindTime = time,
            ChildName = childName,
            CreatedBy = "bot"
        };

        var insertResponse = await _supabase
            .From<Reminder>()
            .Insert(reminder);

        var insertedReminder = insertResponse.Models.FirstOrDefault();
        if (insertedReminder == null)
        {
            throw new InvalidOperationException("Failed to insert reminder");
        }

        _logger.LogInformation("Added reminder with ID {ReminderId}: {Text}", insertedReminder.Id, text);
        return insertedReminder.Id;
    }

    public async Task<List<Reminder>> GetPendingRemindersAsync()
    {
        var now = DateTime.Now;
        var currentDate = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        // Get reminders for today that haven't been sent yet and are due now or in the past
        var reminders = await _supabase
            .From<Reminder>()
            .Select("*")
            .Where(r => r.RemindDate <= currentDate && r.IsSent == false)
            .Get();

        var pendingReminders = reminders.Models
            .Where(r => r.RemindDate < currentDate || (r.RemindDate == currentDate && r.RemindTime <= currentTime))
            .OrderBy(r => r.RemindDate)
            .ThenBy(r => r.RemindTime)
            .ToList();

        _logger.LogInformation("Found {Count} pending reminders", pendingReminders.Count);
        return pendingReminders;
    }

    public async Task MarkReminderAsSentAsync(int reminderId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reminderId, 0);

        await _supabase
            .From<Reminder>()
            .Where(r => r.Id == reminderId)
            .Set(r => r.IsSent, true)
            .Update();

        _logger.LogInformation("Marked reminder {ReminderId} as sent", reminderId);
    }

    public async Task<List<Reminder>> GetAllRemindersAsync()
    {
        var reminders = await _supabase
            .From<Reminder>()
            .Select("*")
            .Order("remind_date", Supabase.Postgrest.Constants.Ordering.Descending)
            .Order("remind_time", Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return reminders.Models;
    }

    public async Task DeleteReminderAsync(int reminderId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reminderId, 0);

        await _supabase
            .From<Reminder>()
            .Where(r => r.Id == reminderId)
            .Delete();

        _logger.LogInformation("Deleted reminder with ID {ReminderId}", reminderId);
    }
}
