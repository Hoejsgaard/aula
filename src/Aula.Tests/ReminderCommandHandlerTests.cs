using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Aula.Tools;
using Aula.Configuration;
using Aula.Services;

namespace Aula.Tests;

public class ReminderCommandHandlerTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Dictionary<string, Child> _childrenByName;
    private readonly ReminderCommandHandler _handler;

    public ReminderCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockSupabaseService = new Mock<ISupabaseService>();
        _childrenByName = new Dictionary<string, Child>
        {
            { "hans", new Child { FirstName = "Hans Jensen", LastName = "Hansen" } },
            { "emma", new Child { FirstName = "Emma Marie", LastName = "Nielsen" } },
            { "liam", new Child { FirstName = "Liam", LastName = "Andersen" } }
        };

        _handler = new ReminderCommandHandler(_mockLogger.Object, _mockSupabaseService.Object, _childrenByName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ReminderCommandHandler(null!, _mockSupabaseService.Object, _childrenByName));
    }

    [Fact]
    public void Constructor_WithNullSupabaseService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ReminderCommandHandler(_mockLogger.Object, null!, _childrenByName));
    }

    [Fact]
    public void Constructor_WithNullChildrenByName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ReminderCommandHandler(_mockLogger.Object, _mockSupabaseService.Object, null!));
    }

    [Theory]
    [InlineData("remind me tomorrow at 8:00 that Hans has Haver til maver", true, "Hans Jensen")]
    [InlineData("husk mig i morgen kl 8:00 at Hans har Haver til maver", false, "Hans Jensen")]
    [InlineData("remind me today at 15:30 that Emma has piano lesson", true, "Emma Marie")]
    [InlineData("remind me 2024-12-31 at 10:00 that New Year celebration", true, null)]
    [InlineData("remind me 31/12 at 23:59 that Year end", true, null)]
    public async Task TryHandleReminderCommand_AddReminder_ValidFormats_ReturnsSuccess(
        string command, bool isEnglish, string? expectedChildName)
    {
        // Arrange
        const int reminderId = 123;
        _mockSupabaseService
            .Setup(x => x.AddReminderAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), expectedChildName))
            .ReturnsAsync(reminderId);

        // Act
        var result = await _handler.TryHandleReminderCommand(command, isEnglish);

        // Assert
        Assert.True(result.handled);
        Assert.NotNull(result.response);
        Assert.Contains($"ID: {reminderId}", result.response);

        if (isEnglish)
        {
            Assert.Contains("✅ Reminder added", result.response);
        }
        else
        {
            Assert.Contains("✅ Påmindelse tilføjet", result.response);
        }

        _mockSupabaseService.Verify(
            x => x.AddReminderAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), expectedChildName),
            Times.Once);
    }

    [Theory]
    [InlineData("remind me tomorrow at 25:00 that invalid time", true)]
    public async Task TryHandleReminderCommand_AddReminder_InvalidFormats_ReturnsErrorMessage(
        string command, bool isEnglish)
    {
        // Arrange
        _mockSupabaseService
            .Setup(x => x.AddReminderAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), It.IsAny<string>()))
            .ThrowsAsync(new FormatException("Invalid format"));

        // Act
        var result = await _handler.TryHandleReminderCommand(command, isEnglish);

        // Assert
        Assert.True(result.handled);
        Assert.NotNull(result.response);

        if (isEnglish)
        {
            Assert.Contains("❌ Failed to add reminder", result.response);
        }
        else
        {
            Assert.Contains("❌ Kunne ikke tilføje påmindelse", result.response);
        }
    }

    [Theory]
    [InlineData("remind me yesterday at 8:00 that past date", true)]
    [InlineData("husk mig ugyldig at 8:00 at invalid format", false)]
    [InlineData("remind me tomorrow at eight that non-numeric time", true)]
    public async Task TryHandleReminderCommand_NonMatchingPatterns_ReturnsFalse(
        string command, bool isEnglish)
    {
        // Act
        var result = await _handler.TryHandleReminderCommand(command, isEnglish);

        // Assert
        Assert.False(result.handled);
        Assert.Null(result.response);

        // Verify no service calls were made
        _mockSupabaseService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryHandleReminderCommand_AddReminder_WithException_LogsErrorAndReturnsErrorMessage()
    {
        // Arrange
        const string command = "remind me tomorrow at 8:00 that test reminder";
        var exception = new Exception("Database error");

        _mockSupabaseService
            .Setup(x => x.AddReminderAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), It.IsAny<string>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _handler.TryHandleReminderCommand(command, true);

        // Assert
        Assert.True(result.handled);
        Assert.Contains("❌ Failed to add reminder", result.response!);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error adding reminder")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("list reminders", true)]
    [InlineData("show reminders", true)]
    [InlineData("vis påmindelser", false)]
    [InlineData("liste påmindelser", false)]
    public async Task TryHandleReminderCommand_ListReminders_ValidCommands_ReturnsRemindersList(
        string command, bool isEnglish)
    {
        // Arrange
        var reminders = new List<Reminder>
        {
            new() {
                Id = 1,
                Text = "Test reminder 1",
                RemindDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                RemindTime = new TimeOnly(8, 0),
                IsSent = false,
                ChildName = "Hans Jensen"
            },
            new() {
                Id = 2,
                Text = "Test reminder 2",
                RemindDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                RemindTime = new TimeOnly(15, 30),
                IsSent = true,
                ChildName = null
            }
        };

        _mockSupabaseService
            .Setup(x => x.GetAllRemindersAsync())
            .ReturnsAsync(reminders);

        // Act
        var result = await _handler.TryHandleReminderCommand(command, isEnglish);

        // Assert
        Assert.True(result.handled);
        Assert.NotNull(result.response);

        if (isEnglish)
        {
            Assert.Contains("📝 <b>Your Reminders:</b>", result.response);
            Assert.Contains("✅ Sent", result.response);
            Assert.Contains("⏳ Pending", result.response);
        }
        else
        {
            Assert.Contains("📝 <b>Dine Påmindelser:</b>", result.response);
            Assert.Contains("✅ Sendt", result.response);
            Assert.Contains("⏳ Afventer", result.response);
        }

        Assert.Contains("ID 1:", result.response);
        Assert.Contains("ID 2:", result.response);
        Assert.Contains("Test reminder 1", result.response);
        Assert.Contains("Test reminder 2", result.response);
        Assert.Contains("(Hans Jensen)", result.response);

        _mockSupabaseService.Verify(x => x.GetAllRemindersAsync(), Times.Once);
    }

    [Theory]
    [InlineData("list reminders", true)]
    [InlineData("vis påmindelser", false)]
    public async Task TryHandleReminderCommand_ListReminders_NoReminders_ReturnsNoRemindersMessage(
        string command, bool isEnglish)
    {
        // Arrange
        var emptyReminders = new List<Reminder>();
        _mockSupabaseService
            .Setup(x => x.GetAllRemindersAsync())
            .ReturnsAsync(emptyReminders);

        // Act
        var result = await _handler.TryHandleReminderCommand(command, isEnglish);

        // Assert
        Assert.True(result.handled);
        Assert.NotNull(result.response);

        if (isEnglish)
        {
            Assert.Contains("📝 No reminders found", result.response);
        }
        else
        {
            Assert.Contains("📝 Ingen påmindelser fundet", result.response);
        }
    }

    [Theory]
    [InlineData("list reminders", true)]
    [InlineData("vis påmindelser", false)]
    public async Task TryHandleReminderCommand_ListReminders_WithException_LogsErrorAndReturnsErrorMessage(
        string command, bool isEnglish)
    {
        // Arrange
        var exception = new Exception("Database connection failed");
        _mockSupabaseService
            .Setup(x => x.GetAllRemindersAsync())
            .ThrowsAsync(exception);

        // Act
        var result = await _handler.TryHandleReminderCommand(command, isEnglish);

        // Assert
        Assert.True(result.handled);
        Assert.NotNull(result.response);

        if (isEnglish)
        {
            Assert.Contains("❌ Failed to retrieve reminders", result.response);
        }
        else
        {
            Assert.Contains("❌ Kunne ikke hente påmindelser", result.response);
        }

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error listing reminders")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("delete reminder 123", true, 123)]
    [InlineData("slet påmindelse 456", false, 456)]
    [InlineData("delete reminder 1", true, 1)]
    public async Task TryHandleReminderCommand_DeleteReminder_ValidCommands_ReturnsSuccess(
        string command, bool isEnglish, int expectedId)
    {
        // Arrange
        _mockSupabaseService
            .Setup(x => x.DeleteReminderAsync(expectedId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.TryHandleReminderCommand(command, isEnglish);

        // Assert
        Assert.True(result.handled);
        Assert.NotNull(result.response);

        if (isEnglish)
        {
            Assert.Contains($"✅ Reminder {expectedId} deleted", result.response);
        }
        else
        {
            Assert.Contains($"✅ Påmindelse {expectedId} slettet", result.response);
        }

        _mockSupabaseService.Verify(x => x.DeleteReminderAsync(expectedId), Times.Once);
    }

    [Theory]
    [InlineData("delete reminder 999", true)]
    [InlineData("slet påmindelse 888", false)]
    public async Task TryHandleReminderCommand_DeleteReminder_WithException_LogsErrorAndReturnsErrorMessage(
        string command, bool isEnglish)
    {
        // Arrange
        var exception = new Exception("Reminder not found");
        _mockSupabaseService
            .Setup(x => x.DeleteReminderAsync(It.IsAny<int>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _handler.TryHandleReminderCommand(command, isEnglish);

        // Assert
        Assert.True(result.handled);
        Assert.NotNull(result.response);

        if (isEnglish)
        {
            Assert.Contains("❌ Failed to delete reminder", result.response);
        }
        else
        {
            Assert.Contains("❌ Kunne ikke slette påmindelse", result.response);
        }

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error deleting reminder")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("how are you?")]
    [InlineData("random text")]
    [InlineData("remind me")]
    [InlineData("delete")]
    [InlineData("list")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TryHandleReminderCommand_NonReminderCommands_ReturnsFalse(string command)
    {
        // Act
        var result = await _handler.TryHandleReminderCommand(command, true);

        // Assert
        Assert.False(result.handled);
        Assert.Null(result.response);

        // Verify no service calls were made
        _mockSupabaseService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryHandleReminderCommand_DateParsing_TomorrowAndToday_WorksCorrectly()
    {
        // Arrange
        const int reminderId = 1;
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var today = DateOnly.FromDateTime(DateTime.Today);

        _mockSupabaseService
            .Setup(x => x.AddReminderAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), It.IsAny<string>()))
            .ReturnsAsync(reminderId);

        // Act - Tomorrow
        var tomorrowResult = await _handler.TryHandleReminderCommand("remind me tomorrow at 8:00 that test", true);

        // Act - Today
        var todayResult = await _handler.TryHandleReminderCommand("remind me today at 8:00 that test", true);

        // Assert
        Assert.True(tomorrowResult.handled);
        Assert.True(todayResult.handled);

        _mockSupabaseService.Verify(
            x => x.AddReminderAsync("test", tomorrow, new TimeOnly(8, 0), null),
            Times.Once);

        _mockSupabaseService.Verify(
            x => x.AddReminderAsync("test", today, new TimeOnly(8, 0), null),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleReminderCommand_DateParsing_DDMM_Format_WorksCorrectly()
    {
        // Arrange
        const int reminderId = 1;
        var expectedDate = new DateOnly(DateTime.Now.Year, 12, 25);

        // If Christmas has passed this year, it should be next year
        if (DateTime.Now > new DateTime(DateTime.Now.Year, 12, 25))
        {
            expectedDate = new DateOnly(DateTime.Now.Year + 1, 12, 25);
        }

        _mockSupabaseService
            .Setup(x => x.AddReminderAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), It.IsAny<string>()))
            .ReturnsAsync(reminderId);

        // Act
        var result = await _handler.TryHandleReminderCommand("remind me 25/12 at 10:00 that Christmas", true);

        // Assert
        Assert.True(result.handled);
        _mockSupabaseService.Verify(
            x => x.AddReminderAsync("Christmas", expectedDate, new TimeOnly(10, 0), null),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleReminderCommand_ChildNameExtraction_WorksCorrectly()
    {
        // Arrange
        const int reminderId = 1;
        _mockSupabaseService
            .Setup(x => x.AddReminderAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), It.IsAny<string>()))
            .ReturnsAsync(reminderId);

        // Act - Test with first name only (Hans from "Hans Jensen")
        var hansResult = await _handler.TryHandleReminderCommand("remind me tomorrow at 8:00 that Hans has soccer", true);

        // Act - Test with first name only (Emma from "Emma Marie")  
        var emmaResult = await _handler.TryHandleReminderCommand("remind me tomorrow at 8:00 that Emma has piano", true);

        // Act - Test with no child name mentioned
        var noChildResult = await _handler.TryHandleReminderCommand("remind me tomorrow at 8:00 that general reminder", true);

        // Assert
        Assert.True(hansResult.handled);
        Assert.True(emmaResult.handled);
        Assert.True(noChildResult.handled);

        _mockSupabaseService.Verify(
            x => x.AddReminderAsync("Hans has soccer", It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), "Hans Jensen"),
            Times.Once);

        _mockSupabaseService.Verify(
            x => x.AddReminderAsync("Emma has piano", It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), "Emma Marie"),
            Times.Once);

        _mockSupabaseService.Verify(
            x => x.AddReminderAsync("general reminder", It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), null),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleReminderCommand_ListReminders_OrdersByDateAndTime()
    {
        // Arrange
        var reminders = new List<Reminder>
        {
            new() {
                Id = 3,
                Text = "Later today",
                RemindDate = DateOnly.FromDateTime(DateTime.Today),
                RemindTime = new TimeOnly(15, 0),
                IsSent = false
            },
            new() {
                Id = 1,
                Text = "Early today",
                RemindDate = DateOnly.FromDateTime(DateTime.Today),
                RemindTime = new TimeOnly(8, 0),
                IsSent = false
            },
            new() {
                Id = 2,
                Text = "Tomorrow",
                RemindDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                RemindTime = new TimeOnly(9, 0),
                IsSent = false
            }
        };

        _mockSupabaseService
            .Setup(x => x.GetAllRemindersAsync())
            .ReturnsAsync(reminders);

        // Act
        var result = await _handler.TryHandleReminderCommand("list reminders", true);

        // Assert
        Assert.True(result.handled);
        Assert.NotNull(result.response);

        // The response should have the reminders ordered by date, then by time
        var responseLines = result.response.Split('\n');
        var id1Index = Array.FindIndex(responseLines, line => line.Contains("ID 1:"));
        var id2Index = Array.FindIndex(responseLines, line => line.Contains("ID 2:"));
        var id3Index = Array.FindIndex(responseLines, line => line.Contains("ID 3:"));

        // ID 1 (early today) should come before ID 3 (later today) should come before ID 2 (tomorrow)
        Assert.True(id1Index < id3Index);
        Assert.True(id3Index < id2Index);
    }
}