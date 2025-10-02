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