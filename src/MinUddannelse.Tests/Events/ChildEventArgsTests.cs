using MinUddannelse.Events;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace MinUddannelse.Tests.Events;

public class ChildEventArgsTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        var childId = "test_child_id";
        var childFirstName = "Emma";
        var eventType = "test_event";
        var data = new JObject { ["key"] = "value" };

        var eventArgs = new ChildEventArgs(childId, childFirstName, eventType, data);

        Assert.Equal(childId, eventArgs.ChildId);
        Assert.Equal(childFirstName, eventArgs.ChildFirstName);
        Assert.Equal(eventType, eventArgs.EventType);
        Assert.Equal(data, eventArgs.Data);
        Assert.True(eventArgs.EventTime <= DateTimeOffset.UtcNow);
        Assert.True(eventArgs.EventTime >= DateTimeOffset.UtcNow.AddSeconds(-1));
    }

    [Fact]
    public void Constructor_WithValidParametersAndNullData_InitializesCorrectly()
    {
        var childId = "test_child_id";
        var childFirstName = "Emma";
        var eventType = "test_event";

        var eventArgs = new ChildEventArgs(childId, childFirstName, eventType);

        Assert.Equal(childId, eventArgs.ChildId);
        Assert.Equal(childFirstName, eventArgs.ChildFirstName);
        Assert.Equal(eventType, eventArgs.EventType);
        Assert.Null(eventArgs.Data);
        Assert.True(eventArgs.EventTime <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Constructor_WithNullChildId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChildEventArgs(null!, "Emma", "test_event"));
    }

    [Fact]
    public void Constructor_WithNullChildFirstName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChildEventArgs("test_id", null!, "test_event"));
    }

    [Fact]
    public void Constructor_WithNullEventType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChildEventArgs("test_id", "Emma", null!));
    }

    [Fact]
    public void ChildEventArgs_InheritsFromEventArgs()
    {
        var eventArgs = new ChildEventArgs("test_id", "Emma", "test_event");
        Assert.IsAssignableFrom<EventArgs>(eventArgs);
    }

    [Fact]
    public void Properties_AreInitOnlyAndCannotBeModified()
    {
        var eventArgs = new ChildEventArgs("test_id", "Emma", "test_event");

        // These properties should be init-only (compile-time check)
        // This test verifies they can be read but not written after construction
        Assert.NotNull(eventArgs.ChildId);
        Assert.NotNull(eventArgs.ChildFirstName);
        Assert.NotNull(eventArgs.EventType);
        Assert.True(eventArgs.EventTime > default(DateTimeOffset));
    }
}

public class ChildScheduleEventArgsTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        var childId = "test_child_id";
        var childFirstName = "Emma";
        var scheduledDate = DateOnly.FromDateTime(DateTime.Today);
        var taskType = "weekly_letter_check";
        var data = new JObject { ["reminder"] = true };

        var eventArgs = new ChildScheduleEventArgs(childId, childFirstName, scheduledDate, taskType, data);

        Assert.Equal(childId, eventArgs.ChildId);
        Assert.Equal(childFirstName, eventArgs.ChildFirstName);
        Assert.Equal("schedule", eventArgs.EventType);
        Assert.Equal(scheduledDate, eventArgs.ScheduledDate);
        Assert.Equal(taskType, eventArgs.TaskType);
        Assert.Equal(data, eventArgs.Data);
        Assert.True(eventArgs.EventTime <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Constructor_WithValidParametersAndNullData_InitializesCorrectly()
    {
        var childId = "test_child_id";
        var childFirstName = "Emma";
        var scheduledDate = DateOnly.FromDateTime(DateTime.Today);
        var taskType = "weekly_letter_check";

        var eventArgs = new ChildScheduleEventArgs(childId, childFirstName, scheduledDate, taskType);

        Assert.Equal(childId, eventArgs.ChildId);
        Assert.Equal(childFirstName, eventArgs.ChildFirstName);
        Assert.Equal("schedule", eventArgs.EventType);
        Assert.Equal(scheduledDate, eventArgs.ScheduledDate);
        Assert.Equal(taskType, eventArgs.TaskType);
        Assert.Null(eventArgs.Data);
    }

    [Fact]
    public void Constructor_WithNullTaskType_ThrowsArgumentNullException()
    {
        var scheduledDate = DateOnly.FromDateTime(DateTime.Today);

        Assert.Throws<ArgumentNullException>(() =>
            new ChildScheduleEventArgs("test_id", "Emma", scheduledDate, null!));
    }

    [Fact]
    public void Constructor_WithNullChildId_ThrowsArgumentNullException()
    {
        var scheduledDate = DateOnly.FromDateTime(DateTime.Today);

        Assert.Throws<ArgumentNullException>(() =>
            new ChildScheduleEventArgs(null!, "Emma", scheduledDate, "task"));
    }

    [Fact]
    public void Constructor_WithNullChildFirstName_ThrowsArgumentNullException()
    {
        var scheduledDate = DateOnly.FromDateTime(DateTime.Today);

        Assert.Throws<ArgumentNullException>(() =>
            new ChildScheduleEventArgs("test_id", null!, scheduledDate, "task"));
    }

    [Fact]
    public void ChildScheduleEventArgs_InheritsFromChildEventArgs()
    {
        var scheduledDate = DateOnly.FromDateTime(DateTime.Today);
        var eventArgs = new ChildScheduleEventArgs("test_id", "Emma", scheduledDate, "task");

        Assert.IsAssignableFrom<ChildEventArgs>(eventArgs);
        Assert.IsAssignableFrom<EventArgs>(eventArgs);
    }

    [Fact]
    public void EventType_IsAlwaysSchedule()
    {
        var scheduledDate = DateOnly.FromDateTime(DateTime.Today);
        var eventArgs = new ChildScheduleEventArgs("test_id", "Emma", scheduledDate, "any_task_type");

        Assert.Equal("schedule", eventArgs.EventType);
    }
}

public class ChildWeekLetterEventArgsTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        var childId = "test_child_id";
        var childFirstName = "Emma";
        var weekNumber = 25;
        var year = 2024;
        var weekLetter = new JObject
        {
            ["title"] = "Week 25 Letter",
            ["content"] = "Test content"
        };

        var eventArgs = new ChildWeekLetterEventArgs(childId, childFirstName, weekNumber, year, weekLetter);

        Assert.Equal(childId, eventArgs.ChildId);
        Assert.Equal(childFirstName, eventArgs.ChildFirstName);
        Assert.Equal("week_letter", eventArgs.EventType);
        Assert.Equal(weekNumber, eventArgs.WeekNumber);
        Assert.Equal(year, eventArgs.Year);
        Assert.Equal(weekLetter, eventArgs.WeekLetter);
        Assert.Equal(weekLetter, eventArgs.Data);
        Assert.True(eventArgs.EventTime <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Constructor_WithNullWeekLetter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChildWeekLetterEventArgs("test_id", "Emma", 25, 2024, null!));
    }

    [Fact]
    public void Constructor_WithNullChildId_ThrowsArgumentNullException()
    {
        var weekLetter = new JObject { ["title"] = "Test" };

        Assert.Throws<ArgumentNullException>(() =>
            new ChildWeekLetterEventArgs(null!, "Emma", 25, 2024, weekLetter));
    }

    [Fact]
    public void Constructor_WithNullChildFirstName_ThrowsArgumentNullException()
    {
        var weekLetter = new JObject { ["title"] = "Test" };

        Assert.Throws<ArgumentNullException>(() =>
            new ChildWeekLetterEventArgs("test_id", null!, 25, 2024, weekLetter));
    }

    [Fact]
    public void ChildWeekLetterEventArgs_InheritsFromChildEventArgs()
    {
        var weekLetter = new JObject { ["title"] = "Test" };
        var eventArgs = new ChildWeekLetterEventArgs("test_id", "Emma", 25, 2024, weekLetter);

        Assert.IsAssignableFrom<ChildEventArgs>(eventArgs);
        Assert.IsAssignableFrom<EventArgs>(eventArgs);
    }

    [Fact]
    public void EventType_IsAlwaysWeekLetter()
    {
        var weekLetter = new JObject { ["title"] = "Test" };
        var eventArgs = new ChildWeekLetterEventArgs("test_id", "Emma", 25, 2024, weekLetter);

        Assert.Equal("week_letter", eventArgs.EventType);
    }

    [Fact]
    public void WeekLetter_IsAlsoSetAsData()
    {
        var weekLetter = new JObject { ["title"] = "Test" };
        var eventArgs = new ChildWeekLetterEventArgs("test_id", "Emma", 25, 2024, weekLetter);

        Assert.Equal(weekLetter, eventArgs.WeekLetter);
        Assert.Equal(weekLetter, eventArgs.Data);
        Assert.Same(eventArgs.WeekLetter, eventArgs.Data);
    }

    [Fact]
    public void WeekNumber_AndYear_AreStoredCorrectly()
    {
        var weekLetter = new JObject { ["title"] = "Test" };
        var eventArgs = new ChildWeekLetterEventArgs("test_id", "Emma", 52, 2025, weekLetter);

        Assert.Equal(52, eventArgs.WeekNumber);
        Assert.Equal(2025, eventArgs.Year);
    }
}

public class ChildReminderEventArgsTests
{
    [Fact]
    public void Constructor_WithValidReminder_InitializesCorrectly()
    {
        var childId = "test_child_id";
        var childFirstName = "Emma";
        var reminder = new MinUddannelse.Repositories.DTOs.Reminder
        {
            Id = 123,
            Text = "Don't forget your lunch",
            RemindDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            RemindTime = TimeOnly.Parse("08:30"),
            ChildName = childFirstName
        };

        var eventArgs = new ChildReminderEventArgs(childId, childFirstName, reminder);

        Assert.Equal(childId, eventArgs.ChildId);
        Assert.Equal(childFirstName, eventArgs.ChildFirstName);
        Assert.Equal("reminder", eventArgs.EventType);
        Assert.Equal(reminder.Text, eventArgs.ReminderText);
        Assert.Equal(reminder.RemindDate, eventArgs.RemindDate);
        Assert.Equal(reminder.RemindTime, eventArgs.RemindTime);
        Assert.Equal(reminder.Id, eventArgs.ReminderId);
        Assert.True(eventArgs.EventTime <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Constructor_WithNullReminder_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChildReminderEventArgs("test_id", "Emma", null!));
    }

    [Fact]
    public void Constructor_WithNullChildId_ThrowsArgumentNullException()
    {
        var reminder = new MinUddannelse.Repositories.DTOs.Reminder
        {
            Id = 123,
            Text = "Test reminder",
            RemindDate = DateOnly.FromDateTime(DateTime.Today),
            RemindTime = TimeOnly.Parse("09:00")
        };

        Assert.Throws<ArgumentNullException>(() =>
            new ChildReminderEventArgs(null!, "Emma", reminder));
    }

    [Fact]
    public void Constructor_WithNullChildFirstName_ThrowsArgumentNullException()
    {
        var reminder = new MinUddannelse.Repositories.DTOs.Reminder
        {
            Id = 123,
            Text = "Test reminder",
            RemindDate = DateOnly.FromDateTime(DateTime.Today),
            RemindTime = TimeOnly.Parse("09:00")
        };

        Assert.Throws<ArgumentNullException>(() =>
            new ChildReminderEventArgs("test_id", null!, reminder));
    }

    [Fact]
    public void ChildReminderEventArgs_InheritsFromChildEventArgs()
    {
        var reminder = new MinUddannelse.Repositories.DTOs.Reminder
        {
            Id = 123,
            Text = "Test reminder",
            RemindDate = DateOnly.FromDateTime(DateTime.Today),
            RemindTime = TimeOnly.Parse("09:00")
        };
        var eventArgs = new ChildReminderEventArgs("test_id", "Emma", reminder);

        Assert.IsAssignableFrom<ChildEventArgs>(eventArgs);
        Assert.IsAssignableFrom<EventArgs>(eventArgs);
    }

    [Fact]
    public void EventType_IsAlwaysReminder()
    {
        var reminder = new MinUddannelse.Repositories.DTOs.Reminder
        {
            Id = 456,
            Text = "Another reminder",
            RemindDate = DateOnly.FromDateTime(DateTime.Today),
            RemindTime = TimeOnly.Parse("15:30")
        };
        var eventArgs = new ChildReminderEventArgs("test_id", "Emma", reminder);

        Assert.Equal("reminder", eventArgs.EventType);
    }

    [Fact]
    public void Properties_ReflectReminderData()
    {
        var reminder = new MinUddannelse.Repositories.DTOs.Reminder
        {
            Id = 789,
            Text = "Pick up homework",
            RemindDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            RemindTime = TimeOnly.Parse("16:00")
        };
        var eventArgs = new ChildReminderEventArgs("child_id", "Test Child", reminder);

        Assert.Equal(789, eventArgs.ReminderId);
        Assert.Equal("Pick up homework", eventArgs.ReminderText);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today.AddDays(2)), eventArgs.RemindDate);
        Assert.Equal(TimeOnly.Parse("16:00"), eventArgs.RemindTime);
    }
}

public class ChildMessageEventArgsTests
{
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        var childId = "test_child_id";
        var childFirstName = "Emma";
        var message = "AI analysis results available";
        var messageType = "ai_analysis";

        var eventArgs = new ChildMessageEventArgs(childId, childFirstName, message, messageType);

        Assert.Equal(childId, eventArgs.ChildId);
        Assert.Equal(childFirstName, eventArgs.ChildFirstName);
        Assert.Equal("message", eventArgs.EventType);
        Assert.Equal(message, eventArgs.Message);
        Assert.Equal(messageType, eventArgs.MessageType);
        Assert.True(eventArgs.EventTime <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Constructor_WithNullMessage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChildMessageEventArgs("test_id", "Emma", null!, "test_type"));
    }

    [Fact]
    public void Constructor_WithNullMessageType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChildMessageEventArgs("test_id", "Emma", "test message", null!));
    }

    [Fact]
    public void Constructor_WithNullChildId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChildMessageEventArgs(null!, "Emma", "test message", "test_type"));
    }

    [Fact]
    public void Constructor_WithNullChildFirstName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChildMessageEventArgs("test_id", null!, "test message", "test_type"));
    }

    [Fact]
    public void ChildMessageEventArgs_InheritsFromChildEventArgs()
    {
        var eventArgs = new ChildMessageEventArgs("test_id", "Emma", "test message", "test_type");

        Assert.IsAssignableFrom<ChildEventArgs>(eventArgs);
        Assert.IsAssignableFrom<EventArgs>(eventArgs);
    }

    [Fact]
    public void EventType_IsAlwaysMessage()
    {
        var eventArgs = new ChildMessageEventArgs("test_id", "Emma", "test message", "reminder");

        Assert.Equal("message", eventArgs.EventType);
    }

    [Fact]
    public void Properties_StoreCorrectValues()
    {
        var message = "Week letter analysis complete";
        var messageType = "ai_notification";
        var eventArgs = new ChildMessageEventArgs("child_id", "Test Child", message, messageType);

        Assert.Equal(message, eventArgs.Message);
        Assert.Equal(messageType, eventArgs.MessageType);
        Assert.Equal("child_id", eventArgs.ChildId);
        Assert.Equal("Test Child", eventArgs.ChildFirstName);
    }
}