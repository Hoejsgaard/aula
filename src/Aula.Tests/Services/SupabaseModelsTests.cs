using Xunit;
using Aula.AI.Services;
using Aula.Content.WeekLetters;
using Aula.Core.Models;
using Aula.Core.Security;
using Aula.Core.Utilities;
using System;

namespace Aula.Tests.Services;

public class ReminderTests
{
    [Fact]
    public void Reminder_DefaultValues_AreSetCorrectly()
    {
        var reminder = new Reminder();

        Assert.Equal(0, reminder.Id);
        Assert.Equal(string.Empty, reminder.Text);
        Assert.Equal(default, reminder.RemindDate);
        Assert.Equal(default, reminder.RemindTime);
        Assert.Equal(default, reminder.CreatedAt);
        Assert.False(reminder.IsSent);
        Assert.Null(reminder.ChildName);
        Assert.Equal("bot", reminder.CreatedBy);
    }

    [Fact]
    public void Reminder_Properties_CanBeSet()
    {
        var reminder = new Reminder
        {
            Id = 123,
            Text = "Test reminder",
            RemindDate = new DateOnly(2025, 10, 1),
            RemindTime = new TimeOnly(14, 30),
            CreatedAt = new DateTime(2025, 10, 1, 10, 0, 0),
            IsSent = true,
            ChildName = "Emma",
            CreatedBy = "user"
        };

        Assert.Equal(123, reminder.Id);
        Assert.Equal("Test reminder", reminder.Text);
        Assert.Equal(new DateOnly(2025, 10, 1), reminder.RemindDate);
        Assert.Equal(new TimeOnly(14, 30), reminder.RemindTime);
        Assert.Equal(new DateTime(2025, 10, 1, 10, 0, 0), reminder.CreatedAt);
        Assert.True(reminder.IsSent);
        Assert.Equal("Emma", reminder.ChildName);
        Assert.Equal("user", reminder.CreatedBy);
    }
}

public class ScheduledTaskTests
{
    [Fact]
    public void ScheduledTask_DefaultValues_AreSetCorrectly()
    {
        var task = new ScheduledTask();

        Assert.Equal(0, task.Id);
        Assert.Equal(string.Empty, task.Name);
        Assert.Null(task.Description);
        Assert.Equal(string.Empty, task.CronExpression);
        Assert.True(task.Enabled);
        Assert.Null(task.RetryIntervalHours);
        Assert.Null(task.MaxRetryHours);
        Assert.Null(task.LastRun);
        Assert.Null(task.NextRun);
        Assert.Equal(default, task.CreatedAt);
        Assert.Equal(default, task.UpdatedAt);
    }

    [Fact]
    public void ScheduledTask_Properties_CanBeSet()
    {
        var task = new ScheduledTask
        {
            Id = 456,
            Name = "FetchWeekLetters",
            Description = "Fetch week letters for all children",
            CronExpression = "0 8 * * 1",
            Enabled = true,
            RetryIntervalHours = 2,
            MaxRetryHours = 24,
            LastRun = new DateTime(2025, 10, 1, 8, 0, 0),
            NextRun = new DateTime(2025, 10, 8, 8, 0, 0),
            CreatedAt = new DateTime(2025, 9, 30, 12, 0, 0),
            UpdatedAt = new DateTime(2025, 10, 1, 8, 0, 0)
        };

        Assert.Equal(456, task.Id);
        Assert.Equal("FetchWeekLetters", task.Name);
        Assert.Equal("Fetch week letters for all children", task.Description);
        Assert.Equal("0 8 * * 1", task.CronExpression);
        Assert.True(task.Enabled);
        Assert.Equal(2, task.RetryIntervalHours);
        Assert.Equal(24, task.MaxRetryHours);
        Assert.Equal(new DateTime(2025, 10, 1, 8, 0, 0), task.LastRun);
        Assert.Equal(new DateTime(2025, 10, 8, 8, 0, 0), task.NextRun);
        Assert.Equal(new DateTime(2025, 9, 30, 12, 0, 0), task.CreatedAt);
        Assert.Equal(new DateTime(2025, 10, 1, 8, 0, 0), task.UpdatedAt);
    }
}

public class StoredWeekLetterTests
{
    [Fact]
    public void StoredWeekLetter_DefaultValues_AreSetCorrectly()
    {
        var letter = new StoredWeekLetter();

        Assert.Equal(string.Empty, letter.ChildName);
        Assert.Equal(0, letter.WeekNumber);
        Assert.Equal(0, letter.Year);
        Assert.Null(letter.RawContent);
        Assert.Equal(default, letter.PostedAt);
    }

    [Fact]
    public void StoredWeekLetter_Properties_CanBeSet()
    {
        var letter = new StoredWeekLetter
        {
            ChildName = "Emma",
            WeekNumber = 40,
            Year = 2025,
            RawContent = "{\"ugebreve\":[]}",
            PostedAt = new DateTime(2025, 10, 1, 7, 0, 0)
        };

        Assert.Equal("Emma", letter.ChildName);
        Assert.Equal(40, letter.WeekNumber);
        Assert.Equal(2025, letter.Year);
        Assert.Equal("{\"ugebreve\":[]}", letter.RawContent);
        Assert.Equal(new DateTime(2025, 10, 1, 7, 0, 0), letter.PostedAt);
    }
}

public class PostedLetterTests
{
    [Fact]
    public void PostedLetter_DefaultValues_AreSetCorrectly()
    {
        var posted = new PostedLetter();

        Assert.Equal(0, posted.Id);
        Assert.Equal(string.Empty, posted.ChildName);
        Assert.Equal(0, posted.WeekNumber);
        Assert.Equal(0, posted.Year);
        Assert.Equal(string.Empty, posted.ContentHash);
        Assert.Equal(default, posted.PostedAt);
        Assert.False(posted.PostedToSlack);
        Assert.False(posted.PostedToTelegram);
        Assert.Null(posted.RawContent);
    }

    [Fact]
    public void PostedLetter_Properties_CanBeSet()
    {
        var posted = new PostedLetter
        {
            Id = 111,
            ChildName = "Lucas",
            WeekNumber = 40,
            Year = 2025,
            ContentHash = "abc123def456",
            PostedAt = new DateTime(2025, 10, 1, 8, 0, 0),
            PostedToSlack = true,
            PostedToTelegram = false,
            RawContent = "{\"content\":\"test\"}"
        };

        Assert.Equal(111, posted.Id);
        Assert.Equal("Lucas", posted.ChildName);
        Assert.Equal(40, posted.WeekNumber);
        Assert.Equal(2025, posted.Year);
        Assert.Equal("abc123def456", posted.ContentHash);
        Assert.Equal(new DateTime(2025, 10, 1, 8, 0, 0), posted.PostedAt);
        Assert.True(posted.PostedToSlack);
        Assert.False(posted.PostedToTelegram);
        Assert.Equal("{\"content\":\"test\"}", posted.RawContent);
    }
}

public class AppStateTests
{
    [Fact]
    public void AppState_DefaultValues_AreSetCorrectly()
    {
        var state = new AppState();

        Assert.Equal(string.Empty, state.Key);
        Assert.Equal(string.Empty, state.Value);
        Assert.Equal(default, state.UpdatedAt);
    }

    [Fact]
    public void AppState_Properties_CanBeSet()
    {
        var state = new AppState
        {
            Key = "last_fetch_time",
            Value = "2025-10-01T08:00:00Z",
            UpdatedAt = new DateTime(2025, 10, 1, 8, 0, 0)
        };

        Assert.Equal("last_fetch_time", state.Key);
        Assert.Equal("2025-10-01T08:00:00Z", state.Value);
        Assert.Equal(new DateTime(2025, 10, 1, 8, 0, 0), state.UpdatedAt);
    }
}

public class RetryAttemptTests
{
    [Fact]
    public void RetryAttempt_DefaultValues_AreSetCorrectly()
    {
        var retry = new RetryAttempt();

        Assert.Equal(0, retry.Id);
        Assert.Equal(string.Empty, retry.ChildName);
        Assert.Equal(0, retry.WeekNumber);
        Assert.Equal(0, retry.Year);
        Assert.Equal(0, retry.AttemptCount);
        Assert.Equal(default, retry.LastAttempt);
        Assert.Null(retry.NextAttempt);
        Assert.Equal(0, retry.MaxAttempts);
        Assert.False(retry.IsSuccessful);
    }

    [Fact]
    public void RetryAttempt_Properties_CanBeSet()
    {
        var retry = new RetryAttempt
        {
            Id = 333,
            ChildName = "Emma",
            WeekNumber = 40,
            Year = 2025,
            AttemptCount = 2,
            LastAttempt = new DateTime(2025, 10, 1, 9, 0, 0),
            NextAttempt = new DateTime(2025, 10, 1, 11, 0, 0),
            MaxAttempts = 5,
            IsSuccessful = false
        };

        Assert.Equal(333, retry.Id);
        Assert.Equal("Emma", retry.ChildName);
        Assert.Equal(40, retry.WeekNumber);
        Assert.Equal(2025, retry.Year);
        Assert.Equal(2, retry.AttemptCount);
        Assert.Equal(new DateTime(2025, 10, 1, 9, 0, 0), retry.LastAttempt);
        Assert.Equal(new DateTime(2025, 10, 1, 11, 0, 0), retry.NextAttempt);
        Assert.Equal(5, retry.MaxAttempts);
        Assert.False(retry.IsSuccessful);
    }
}
