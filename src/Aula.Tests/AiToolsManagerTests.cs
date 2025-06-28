using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;

namespace Aula.Tests;

public class AiToolsManagerTests
{
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Mock<IDataManager> _mockDataManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AiToolsManager _aiToolsManager;
    private readonly List<Child> _testChildren;

    public AiToolsManagerTests()
    {
        _mockSupabaseService = new Mock<ISupabaseService>();
        _mockDataManager = new Mock<IDataManager>();
        _loggerFactory = new LoggerFactory();

        _testChildren = new List<Child>
        {
            new Child { FirstName = "Alice", LastName = "Johnson" },
            new Child { FirstName = "Bob", LastName = "Smith" }
        };

        _mockDataManager.Setup(d => d.GetChildren()).Returns(_testChildren);

        _aiToolsManager = new AiToolsManager(_mockSupabaseService.Object, _mockDataManager.Object, _loggerFactory);
    }

    [Fact]
    public async Task CreateReminderAsync_WithValidDateTime_CreatesReminder()
    {
        // Arrange
        var description = "Pick up Alice from school";
        var dateTime = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd HH:mm");
        var childName = "Alice";

        _mockSupabaseService.Setup(s => s.AddReminderAsync(
            It.IsAny<string>(), 
            It.IsAny<DateOnly>(), 
            It.IsAny<TimeOnly>(), 
            It.IsAny<string>()))
            .ReturnsAsync(123);

        // Act
        var result = await _aiToolsManager.CreateReminderAsync(description, dateTime, childName);

        // Assert
        Assert.Contains("✅", result);
        Assert.Contains("Pick up Alice from school", result);
        _mockSupabaseService.Verify(s => s.AddReminderAsync(
            description,
            It.IsAny<DateOnly>(),
            It.IsAny<TimeOnly>(),
            childName), Times.Once);
    }

    [Fact]
    public async Task CreateReminderAsync_WithInvalidDateTime_ReturnsError()
    {
        // Arrange
        var description = "Invalid reminder";
        var invalidDateTime = "not-a-date";
        var childName = "Alice";

        // Act
        var result = await _aiToolsManager.CreateReminderAsync(description, invalidDateTime, childName);

        // Assert
        Assert.Contains("Error: Invalid date format", result);
        _mockSupabaseService.Verify(s => s.AddReminderAsync(
            It.IsAny<string>(), 
            It.IsAny<DateOnly>(), 
            It.IsAny<TimeOnly>(), 
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ListRemindersAsync_WithReminders_ReturnsFormattedList()
    {
        // Arrange
        var testReminders = new List<Reminder>
        {
            new Reminder 
            { 
                Id = 1, 
                Text = "Reminder 1", 
                RemindDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                RemindTime = new TimeOnly(9, 0),
                ChildName = "Alice",
                IsSent = false
            },
            new Reminder 
            { 
                Id = 2, 
                Text = "Reminder 2", 
                RemindDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                RemindTime = new TimeOnly(14, 30),
                ChildName = "Bob",
                IsSent = true
            }
        };

        _mockSupabaseService.Setup(s => s.GetAllRemindersAsync())
            .ReturnsAsync(testReminders);

        // Act
        var result = await _aiToolsManager.ListRemindersAsync();

        // Assert
        Assert.Contains("📋 Active reminders:", result);
        Assert.Contains("Reminder 1", result);
        Assert.Contains("Reminder 2", result);
        Assert.Contains("(Alice)", result);
        Assert.Contains("(Bob)", result);
    }

    [Fact]
    public async Task ListRemindersAsync_WithNoReminders_ReturnsEmptyMessage()
    {
        // Arrange
        _mockSupabaseService.Setup(s => s.GetAllRemindersAsync())
            .ReturnsAsync(new List<Reminder>());

        // Act
        var result = await _aiToolsManager.ListRemindersAsync();

        // Assert
        Assert.Contains("No active reminders found", result);
    }

    [Fact]
    public async Task DeleteReminderAsync_WithValidNumber_DeletesReminder()
    {
        // Arrange
        var reminderNumber = 1; // 1-based index, not ID
        var testReminders = new List<Reminder>
        {
            new Reminder { Id = 123, Text = "Test reminder to delete", RemindDate = DateOnly.FromDateTime(DateTime.Today), RemindTime = new TimeOnly(10, 0) }
        };

        _mockSupabaseService.Setup(s => s.GetAllRemindersAsync())
            .ReturnsAsync(testReminders);

        // Act
        var result = await _aiToolsManager.DeleteReminderAsync(reminderNumber);

        // Assert
        Assert.Contains("✅ Deleted reminder", result);
        _mockSupabaseService.Verify(s => s.DeleteReminderAsync(123), Times.Once); // Verify actual ID is used
    }

    [Fact]
    public async Task DeleteReminderAsync_WithInvalidId_ReturnsError()
    {
        // Arrange
        var invalidId = 999;
        _mockSupabaseService.Setup(s => s.GetAllRemindersAsync())
            .ReturnsAsync(new List<Reminder>());

        // Act
        var result = await _aiToolsManager.DeleteReminderAsync(invalidId);

        // Assert
        Assert.Contains("❌ Invalid reminder number", result);
        _mockSupabaseService.Verify(s => s.DeleteReminderAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetWeekLetters_WithChildName_ReturnsFilteredResults()
    {
        // Arrange
        var weekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "3A",
                    ["uge"] = "42",
                    ["indhold"] = "Test week letter content for Alice"
                }
            }
        };

        _mockDataManager.Setup(d => d.GetWeekLetter(It.Is<Child>(c => c.FirstName == "Alice")))
            .Returns(weekLetter);
        _mockDataManager.Setup(d => d.GetWeekLetter(It.Is<Child>(c => c.FirstName == "Bob")))
            .Returns((JObject?)null);

        // Act
        var result = _aiToolsManager.GetWeekLetters("Alice");

        // Assert
        Assert.Contains("Alice Johnson", result);
        Assert.Contains("Week Letter", result);
        Assert.DoesNotContain("Bob Smith", result);
    }

    [Fact]
    public void GetWeekLetters_WithNonExistentChild_ReturnsError()
    {
        // Act
        var result = _aiToolsManager.GetWeekLetters("NonExistentChild");

        // Assert
        Assert.Contains("❌", result);
        Assert.Contains("No children found matching", result);
    }

    [Fact]
    public async Task CreateReminderAsync_WithMissingChildName_CreatesGeneralReminder()
    {
        // Arrange
        var description = "General reminder";
        var dateTime = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd HH:mm");

        _mockSupabaseService.Setup(s => s.AddReminderAsync(
            It.IsAny<string>(), 
            It.IsAny<DateOnly>(), 
            It.IsAny<TimeOnly>(), 
            It.IsAny<string>()))
            .ReturnsAsync(456);

        // Act
        var result = await _aiToolsManager.CreateReminderAsync(description, dateTime);

        // Assert
        Assert.Contains("✅", result);
        Assert.Contains("General reminder", result);
        _mockSupabaseService.Verify(s => s.AddReminderAsync(
            description,
            It.IsAny<DateOnly>(),
            It.IsAny<TimeOnly>(),
            null), Times.Once);
    }
}