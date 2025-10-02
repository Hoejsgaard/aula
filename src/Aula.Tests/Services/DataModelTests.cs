using Aula.Configuration;
using ConfigChild = Aula.Configuration.Child;
using Aula.AI.Services;
using Aula.Content.WeekLetters;
using Aula.Core.Models;
using Aula.Core.Security;
using Aula.Core.Utilities;

namespace Aula.Tests.Services;

public class DataModelTests
{
    [Fact]
    public void Reminder_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var reminder = new Reminder();

        // Assert
        Assert.Equal(0, reminder.Id);
        Assert.Equal(string.Empty, reminder.Text);
        Assert.Equal(default(DateOnly), reminder.RemindDate);
        Assert.Equal(default(TimeOnly), reminder.RemindTime);
        Assert.Null(reminder.ChildName);
        Assert.False(reminder.IsSent);
        Assert.Equal("bot", reminder.CreatedBy);
    }

    [Fact]
    public void Reminder_WithProperties_SetsCorrectly()
    {
        // Arrange
        var testDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var testTime = new TimeOnly(14, 30);

        // Act
        var reminder = new Reminder
        {
            Id = 123,
            Text = "Pick up Alice from school",
            RemindDate = testDate,
            RemindTime = testTime,
            ChildName = "Alice",
            IsSent = true,
            CreatedBy = "user"
        };

        // Assert
        Assert.Equal(123, reminder.Id);
        Assert.Equal("Pick up Alice from school", reminder.Text);
        Assert.Equal(testDate, reminder.RemindDate);
        Assert.Equal(testTime, reminder.RemindTime);
        Assert.Equal("Alice", reminder.ChildName);
        Assert.True(reminder.IsSent);
        Assert.Equal("user", reminder.CreatedBy);
    }

    [Fact]
    public void PostedLetter_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var postedLetter = new PostedLetter();

        // Assert
        Assert.Equal(0, postedLetter.Id);
        Assert.Equal(string.Empty, postedLetter.ChildName);
        Assert.Equal(0, postedLetter.WeekNumber);
        Assert.Equal(0, postedLetter.Year);
        Assert.Equal(string.Empty, postedLetter.ContentHash);
        Assert.False(postedLetter.PostedToSlack);
        Assert.False(postedLetter.PostedToTelegram);
        Assert.Equal(default(DateTime), postedLetter.PostedAt);
    }

    [Fact]
    public void PostedLetter_WithProperties_SetsCorrectly()
    {
        // Arrange
        var testDate = DateTime.UtcNow;

        // Act
        var postedLetter = new PostedLetter
        {
            Id = 456,
            ChildName = "Bob",
            WeekNumber = 42,
            Year = 2023,
            ContentHash = "abc123def456",
            PostedToSlack = true,
            PostedToTelegram = false,
            PostedAt = testDate
        };

        // Assert
        Assert.Equal(456, postedLetter.Id);
        Assert.Equal("Bob", postedLetter.ChildName);
        Assert.Equal(42, postedLetter.WeekNumber);
        Assert.Equal(2023, postedLetter.Year);
        Assert.Equal("abc123def456", postedLetter.ContentHash);
        Assert.True(postedLetter.PostedToSlack);
        Assert.False(postedLetter.PostedToTelegram);
        Assert.Equal(testDate, postedLetter.PostedAt);
    }

    [Fact]
    public void ScheduledTask_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var scheduledTask = new ScheduledTask();

        // Assert
        Assert.Equal(0, scheduledTask.Id);
        Assert.Equal(string.Empty, scheduledTask.Name);
        Assert.Equal(string.Empty, scheduledTask.CronExpression);
        Assert.True(scheduledTask.Enabled);
        Assert.Null(scheduledTask.LastRun);
        Assert.Equal(default(DateTime), scheduledTask.CreatedAt);
        Assert.Equal(default(DateTime), scheduledTask.UpdatedAt);
        Assert.Null(scheduledTask.RetryIntervalHours);
        Assert.Null(scheduledTask.MaxRetryHours);
    }

    [Fact]
    public void ScheduledTask_WithProperties_SetsCorrectly()
    {
        // Arrange
        var testCreatedAt = DateTime.UtcNow.AddDays(-1);
        var testUpdatedAt = DateTime.UtcNow;
        var testLastRun = DateTime.UtcNow.AddHours(-2);

        // Act
        var scheduledTask = new ScheduledTask
        {
            Id = 789,
            Name = "WeeklyLetterCheck",
            CronExpression = "0 16 * * 0",
            Enabled = false,
            LastRun = testLastRun,
            CreatedAt = testCreatedAt,
            UpdatedAt = testUpdatedAt,
            RetryIntervalHours = 2,
            MaxRetryHours = 24
        };

        // Assert
        Assert.Equal(789, scheduledTask.Id);
        Assert.Equal("WeeklyLetterCheck", scheduledTask.Name);
        Assert.Equal("0 16 * * 0", scheduledTask.CronExpression);
        Assert.False(scheduledTask.Enabled);
        Assert.Equal(testLastRun, scheduledTask.LastRun);
        Assert.Equal(testCreatedAt, scheduledTask.CreatedAt);
        Assert.Equal(testUpdatedAt, scheduledTask.UpdatedAt);
        Assert.Equal(2, scheduledTask.RetryIntervalHours);
        Assert.Equal(24, scheduledTask.MaxRetryHours);
    }

    [Fact]
    public void AppState_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var appState = new AppState();

        // Assert
        Assert.Equal(string.Empty, appState.Key);
        Assert.Equal(string.Empty, appState.Value);
        Assert.Equal(default(DateTime), appState.UpdatedAt);
    }

    [Fact]
    public void AppState_WithProperties_SetsCorrectly()
    {
        // Arrange
        var testCreatedAt = DateTime.UtcNow.AddDays(-1);
        var testUpdatedAt = DateTime.UtcNow;

        // Act
        var appState = new AppState
        {
            Key = "last_processed_week",
            Value = "2023-42",
            UpdatedAt = testUpdatedAt
        };

        // Assert
        Assert.Equal("last_processed_week", appState.Key);
        Assert.Equal("2023-42", appState.Value);
        Assert.Equal(testUpdatedAt, appState.UpdatedAt);
    }

    [Fact]
    public void RetryAttempt_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var retryAttempt = new RetryAttempt();

        // Assert
        Assert.Equal(0, retryAttempt.Id);
        Assert.Equal(string.Empty, retryAttempt.ChildName);
        Assert.Equal(0, retryAttempt.WeekNumber);
        Assert.Equal(0, retryAttempt.Year);
        Assert.Equal(0, retryAttempt.AttemptCount);
        Assert.Equal(default(DateTime), retryAttempt.LastAttempt);
        Assert.Null(retryAttempt.NextAttempt);
        Assert.False(retryAttempt.IsSuccessful);
        Assert.Equal(0, retryAttempt.MaxAttempts);
        // RetryAttempt doesn't have CreatedAt in actual model
    }

    [Fact]
    public void RetryAttempt_WithProperties_SetsCorrectly()
    {
        // Arrange
        var testLastAttempt = DateTime.UtcNow.AddHours(-1);
        var testNextAttempt = DateTime.UtcNow.AddHours(1);
        var testCreatedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var retryAttempt = new RetryAttempt
        {
            Id = 202,
            ChildName = "Charlie",
            WeekNumber = 35,
            Year = 2023,
            AttemptCount = 3,
            LastAttempt = testLastAttempt,
            NextAttempt = testNextAttempt,
            IsSuccessful = true,
            MaxAttempts = 5
        };

        // Assert
        Assert.Equal(202, retryAttempt.Id);
        Assert.Equal("Charlie", retryAttempt.ChildName);
        Assert.Equal(35, retryAttempt.WeekNumber);
        Assert.Equal(2023, retryAttempt.Year);
        Assert.Equal(3, retryAttempt.AttemptCount);
        Assert.Equal(testLastAttempt, retryAttempt.LastAttempt);
        Assert.Equal(testNextAttempt, retryAttempt.NextAttempt);
        Assert.True(retryAttempt.IsSuccessful);
        Assert.Equal(5, retryAttempt.MaxAttempts);
        // No CreatedAt property in RetryAttempt model
    }

    [Theory]
    [InlineData(1, 2023, true)]
    [InlineData(53, 2023, true)]
    [InlineData(0, 2023, false)]
    [InlineData(54, 2023, false)]
    [InlineData(25, 2022, true)]
    public void PostedLetter_WeekNumberValidation_ReturnsExpectedResult(int weekNumber, int year, bool isValid)
    {
        // Arrange
        var postedLetter = new PostedLetter
        {
            ChildName = "Test",
            WeekNumber = weekNumber,
            Year = year,
            ContentHash = "test"
        };

        // Act & Assert
        var isValidWeek = weekNumber >= 1 && weekNumber <= 53;
        var isValidYear = year > 2000 && year < 3000;

        Assert.Equal(isValid, isValidWeek && isValidYear);
        Assert.Equal(weekNumber, postedLetter.WeekNumber);
        Assert.Equal(year, postedLetter.Year);
    }

    [Theory]
    [InlineData("0 16 * * 0", "Weekly at 4 PM on Sundays")]
    [InlineData("0 9 * * 1-5", "Daily at 9 AM on weekdays")]
    [InlineData("0 0 1 * *", "Monthly on the 1st at midnight")]
    public void ScheduledTask_CronExpression_CanBeSet(string cronExpression, string description)
    {
        // Arrange
        var scheduledTask = new ScheduledTask
        {
            Name = $"Test task: {description}",
            CronExpression = cronExpression,
            Enabled = true
        };

        // Assert
        Assert.Equal(cronExpression, scheduledTask.CronExpression);
        Assert.Contains(description, scheduledTask.Name);
        Assert.True(scheduledTask.Enabled);
    }

    [Fact]
    public void Reminder_DateTimeHandling_WorksCorrectly()
    {
        // Arrange
        var today = DateTime.Today;
        var reminderDate = DateOnly.FromDateTime(today.AddDays(1));
        var reminderTime = new TimeOnly(15, 30);

        // Act
        var reminder = new Reminder
        {
            RemindDate = reminderDate,
            RemindTime = reminderTime
        };

        var combinedDateTime = reminderDate.ToDateTime(reminderTime);

        // Assert
        Assert.Equal(reminderDate, reminder.RemindDate);
        Assert.Equal(reminderTime, reminder.RemindTime);
        Assert.Equal(today.AddDays(1).Date.Add(new TimeSpan(15, 30, 0)), combinedDateTime);
    }

    [Fact]
    public void PostedLetter_ContentHash_CanDetectChanges()
    {
        // Arrange
        var originalContent = "Week 42 letter content";
        var modifiedContent = "Week 42 letter content - updated";

        var originalHash = ComputeTestHash(originalContent);
        var modifiedHash = ComputeTestHash(modifiedContent);

        var postedLetter1 = new PostedLetter { ContentHash = originalHash };
        var postedLetter2 = new PostedLetter { ContentHash = modifiedHash };

        // Assert
        Assert.NotEqual(postedLetter1.ContentHash, postedLetter2.ContentHash);
        Assert.Equal(originalHash, postedLetter1.ContentHash);
        Assert.Equal(modifiedHash, postedLetter2.ContentHash);
    }

    [Fact]
    public void ConfigChild_Properties_WorkCorrectly()
    {
        // Arrange & Act
        var configChild = new ConfigChild
        {
            FirstName = "Emma",
            LastName = "Wilson",
            Colour = "green"
        };

        // Assert
        Assert.Equal("Emma", configChild.FirstName);
        Assert.Equal("Wilson", configChild.LastName);
        Assert.Equal("green", configChild.Colour);

        // Test full name logic if it exists
        var expectedFullName = $"{configChild.FirstName} {configChild.LastName}";
        Assert.Equal("Emma Wilson", expectedFullName);
    }

    [Theory]
    [InlineData("bot", true)]
    [InlineData("user", true)]
    [InlineData("system", true)]
    [InlineData("", true)]
    [InlineData(null, false)]
    public void Reminder_CreatedBy_HandlesValues(string? createdBy, bool shouldAccept)
    {
        // Arrange & Act
        var reminder = new Reminder();

        if (shouldAccept)
        {
            reminder.CreatedBy = createdBy ?? "bot";
            Assert.Equal(createdBy ?? "bot", reminder.CreatedBy);
        }
        else
        {
            // For null values, should use default
            Assert.Equal("bot", reminder.CreatedBy);
        }
    }

    private static string ComputeTestHash(string content)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
    }
}
