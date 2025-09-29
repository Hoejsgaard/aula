using Aula.Authentication;
using Aula.Channels;
using Aula.Configuration;
using Aula.Context;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aula.Tests.Channels;

public class SecureChildChannelManagerTests
{
    private readonly Mock<IChildContext> _mockContext;
    private readonly Mock<IChildContextValidator> _mockValidator;
    private readonly Mock<IChildAuditService> _mockAuditService;
    private readonly Mock<IChannelManager> _mockChannelManager;
    private readonly Mock<IMessageContentFilter> _mockContentFilter;
    private readonly Mock<ILogger<SecureChildChannelManager>> _mockLogger;
    private readonly SecureChildChannelManager _manager;
    private readonly Child _testChild;

    public SecureChildChannelManagerTests()
    {
        _mockContext = new Mock<IChildContext>();
        _mockValidator = new Mock<IChildContextValidator>();
        _mockAuditService = new Mock<IChildAuditService>();
        _mockChannelManager = new Mock<IChannelManager>();
        _mockContentFilter = new Mock<IMessageContentFilter>();
        _mockLogger = new Mock<ILogger<SecureChildChannelManager>>();

        _testChild = new Child { FirstName = "Test", LastName = "Child" };
        _mockContext.Setup(c => c.CurrentChild).Returns(_testChild);
        _mockContext.Setup(c => c.ValidateContext()).Verifiable();

        // Setup audit service to not throw
        _mockAuditService.Setup(a => a.LogDataAccessAsync(It.IsAny<Child>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        _mockAuditService.Setup(a => a.LogSecurityEventAsync(It.IsAny<Child>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SecuritySeverity>()))
            .Returns(Task.CompletedTask);

        _manager = new SecureChildChannelManager(
            _mockContext.Object,
            _mockValidator.Object,
            _mockAuditService.Object,
            _mockChannelManager.Object,
            _mockContentFilter.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task SendMessageAsync_ValidatesContext_BeforeSending()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:send"))
            .ReturnsAsync(true);
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);
        _mockContentFilter.Setup(f => f.FilterForChild(It.IsAny<string>(), _testChild))
            .Returns<string, Child>((msg, _) => msg);
        _mockContentFilter.Setup(f => f.ValidateMessageSafety(It.IsAny<string>(), _testChild))
            .Returns(true);

        // Configure a channel for the test child
        var slackConfig = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "test-channel",
            IsEnabled = true
        };
        _manager.ConfigureChildChannel(_testChild, slackConfig);

        var mockChannel = new Mock<IChannel>();
        mockChannel.Setup(c => c.IsEnabled).Returns(true);
        mockChannel.Setup(c => c.FormatMessage(It.IsAny<string>(), It.IsAny<MessageFormat>()))
            .Returns<string, MessageFormat>((msg, _) => msg);
        mockChannel.Setup(c => c.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockChannelManager.Setup(m => m.GetChannel("slack"))
            .Returns(mockChannel.Object);

        // Act
        await _manager.SendMessageAsync("Test message");

        // Assert
        _mockContext.Verify(c => c.ValidateContext(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendMessageAsync_DeniesAccess_WhenNoPermission()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:send"))
            .ReturnsAsync(false);

        // Act
        var result = await _manager.SendMessageAsync("Test message");

        // Assert
        Assert.False(result);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(
            _testChild, "PermissionDenied", "channel:send", SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_FiltersContent_BeforeSending()
    {
        // Arrange
        var originalMessage = "Message with other child data";
        var filteredMessage = "Message with [REDACTED] data";

        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:send"))
            .ReturnsAsync(true);
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);
        _mockContentFilter.Setup(f => f.FilterForChild(originalMessage, _testChild))
            .Returns(filteredMessage);
        _mockContentFilter.Setup(f => f.ValidateMessageSafety(filteredMessage, _testChild))
            .Returns(true);

        // Configure channel
        var config = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "test-channel",
            IsEnabled = true
        };
        _manager.ConfigureChildChannel(_testChild, config);

        var mockChannel = new Mock<IChannel>();
        mockChannel.Setup(c => c.IsEnabled).Returns(true);
        mockChannel.Setup(c => c.FormatMessage(It.IsAny<string>(), It.IsAny<MessageFormat>()))
            .Returns<string, MessageFormat>((msg, _) => msg);
        mockChannel.Setup(c => c.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockChannelManager.Setup(m => m.GetChannel("slack"))
            .Returns(mockChannel.Object);

        // Act
        await _manager.SendMessageAsync(originalMessage);

        // Assert
        mockChannel.Verify(c => c.SendMessageAsync("test-channel", filteredMessage), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_RejectsUnsafeContent()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:send"))
            .ReturnsAsync(true);
        _mockContentFilter.Setup(f => f.FilterForChild(It.IsAny<string>(), _testChild))
            .Returns<string, Child>((msg, _) => msg);
        _mockContentFilter.Setup(f => f.ValidateMessageSafety(It.IsAny<string>(), _testChild))
            .Returns(false);

        // Act
        var result = await _manager.SendMessageAsync("Unsafe message");

        // Assert
        Assert.False(result);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(
            _testChild, "UnsafeMessage", "channel:send", SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task SendToPlatformAsync_OnlySendsToChildChannels()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:send"))
            .ReturnsAsync(true);
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);
        _mockContentFilter.Setup(f => f.FilterForChild(It.IsAny<string>(), _testChild))
            .Returns<string, Child>((msg, _) => msg);
        _mockContentFilter.Setup(f => f.ValidateMessageSafety(It.IsAny<string>(), _testChild))
            .Returns(true);

        // Configure channel for test child
        var config = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "child-channel",
            IsEnabled = true
        };
        _manager.ConfigureChildChannel(_testChild, config);

        var mockChannel = new Mock<IChannel>();
        mockChannel.Setup(c => c.IsEnabled).Returns(true);
        mockChannel.Setup(c => c.FormatMessage(It.IsAny<string>(), It.IsAny<MessageFormat>()))
            .Returns<string, MessageFormat>((msg, _) => msg);
        mockChannel.Setup(c => c.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockChannelManager.Setup(m => m.GetChannel("slack"))
            .Returns(mockChannel.Object);

        // Act
        var result = await _manager.SendToPlatformAsync("slack", "Test message");

        // Assert
        Assert.True(result);
        mockChannel.Verify(c => c.SendMessageAsync("child-channel", "Test message"), Times.Once);
    }

    [Fact]
    public async Task SendToPlatformAsync_DeniesUnauthorizedPlatform()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:send"))
            .ReturnsAsync(true);

        // Configure only Slack for this child
        var config = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "child-channel",
            IsEnabled = true
        };
        _manager.ConfigureChildChannel(_testChild, config);

        // Act - Try to send to Telegram which isn't configured
        var result = await _manager.SendToPlatformAsync("telegram", "Test message");

        // Assert
        Assert.False(result);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(
            _testChild, "UnauthorizedPlatform", "telegram", SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task GetChildChannelsAsync_ReturnsOnlyChildChannels()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);

        // Configure channels for test child
        var slackConfig = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "child-slack",
            IsEnabled = true
        };
        var telegramConfig = new ChildChannelConfig
        {
            PlatformId = "telegram",
            ChannelId = "child-telegram",
            IsEnabled = false
        };
        _manager.ConfigureChildChannel(_testChild, slackConfig);
        _manager.ConfigureChildChannel(_testChild, telegramConfig);

        // Configure channels for another child
        var otherChild = new Child { FirstName = "Other", LastName = "Child" };
        var otherConfig = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "other-slack",
            IsEnabled = true
        };
        _manager.ConfigureChildChannel(otherChild, otherConfig);

        // Act
        var channels = await _manager.GetChildChannelsAsync();

        // Assert
        Assert.Equal(2, channels.Count);
        Assert.Contains(channels, c => c.ChannelId == "child-slack");
        Assert.Contains(channels, c => c.ChannelId == "child-telegram");
        Assert.DoesNotContain(channels, c => c.ChannelId == "other-slack");
    }

    [Fact]
    public async Task GetChildChannelsAsync_ReturnsDefensiveCopies()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);

        var config = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "test-channel",
            IsEnabled = true,
            Metadata = new Dictionary<string, string> { { "key", "value" } }
        };
        _manager.ConfigureChildChannel(_testChild, config);

        // Act
        var channels1 = await _manager.GetChildChannelsAsync();
        var channels2 = await _manager.GetChildChannelsAsync();

        // Modify the first result
        channels1[0].ChannelId = "modified";
        channels1[0].Metadata["key"] = "modified";

        // Assert - Second result should be unmodified
        Assert.Equal("test-channel", channels2[0].ChannelId);
        Assert.Equal("value", channels2[0].Metadata["key"]);
    }

    [Fact]
    public async Task TestChildChannelsAsync_TestsOnlyChildChannels()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);

        var config = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "child-channel",
            IsEnabled = true
        };
        _manager.ConfigureChildChannel(_testChild, config);

        var mockChannel = new Mock<IChannel>();
        mockChannel.Setup(c => c.TestConnectionAsync()).ReturnsAsync(true);
        _mockChannelManager.Setup(m => m.GetChannel("slack"))
            .Returns(mockChannel.Object);

        // Act
        var results = await _manager.TestChildChannelsAsync();

        // Assert
        Assert.Single(results);
        Assert.True(results["slack"]);
        _mockAuditService.Verify(a => a.LogDataAccessAsync(
            _testChild, "TestChannels", "Count:1", true), Times.Once);
    }

    [Fact]
    public async Task SendAlertAsync_RequiresAlertPermission()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:alert"))
            .ReturnsAsync(false);

        // Act
        var result = await _manager.SendAlertAsync("Alert message");

        // Assert
        Assert.False(result);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(
            _testChild, "PermissionDenied", "channel:alert", SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task SendAlertAsync_OnlySendsToAlertEnabledChannels()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:alert"))
            .ReturnsAsync(true);
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);
        _mockContentFilter.Setup(f => f.FilterForChild(It.IsAny<string>(), _testChild))
            .Returns<string, Child>((msg, _) => msg);

        // Configure two channels - only one with alert permission
        var alertChannel = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "alert-channel",
            IsEnabled = true,
            Permissions = new ChannelPermissions { CanReceiveAlerts = true }
        };
        var normalChannel = new ChildChannelConfig
        {
            PlatformId = "telegram",
            ChannelId = "normal-channel",
            IsEnabled = true,
            Permissions = new ChannelPermissions { CanReceiveAlerts = false }
        };
        _manager.ConfigureChildChannel(_testChild, alertChannel);
        _manager.ConfigureChildChannel(_testChild, normalChannel);

        var mockSlackChannel = new Mock<IChannel>();
        mockSlackChannel.Setup(c => c.IsEnabled).Returns(true);
        _mockChannelManager.Setup(m => m.GetChannel("slack"))
            .Returns(mockSlackChannel.Object);

        // Act
        var result = await _manager.SendAlertAsync("Alert!");

        // Assert
        Assert.True(result);
        mockSlackChannel.Verify(c => c.SendMessageAsync(
            "alert-channel",
            It.Is<string>(msg => msg.Contains("⚠️ ALERT"))), Times.Once);
        _mockChannelManager.Verify(m => m.GetChannel("telegram"), Times.Never);
    }

    [Fact]
    public async Task SendReminderAsync_OnlySendsToReminderEnabledChannels()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:reminder"))
            .ReturnsAsync(true);
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);
        _mockContentFilter.Setup(f => f.FilterForChild(It.IsAny<string>(), _testChild))
            .Returns<string, Child>((msg, _) => msg);

        var reminderChannel = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "reminder-channel",
            IsEnabled = true,
            Permissions = new ChannelPermissions { CanReceiveReminders = true }
        };
        _manager.ConfigureChildChannel(_testChild, reminderChannel);

        var mockChannel = new Mock<IChannel>();
        mockChannel.Setup(c => c.IsEnabled).Returns(true);
        _mockChannelManager.Setup(m => m.GetChannel("slack"))
            .Returns(mockChannel.Object);

        // Act
        var result = await _manager.SendReminderAsync("Remember this", "metadata");

        // Assert
        Assert.True(result);
        mockChannel.Verify(c => c.SendMessageAsync(
            "reminder-channel",
            It.Is<string>(msg => msg.Contains("📅 Reminder") && msg.Contains("metadata"))), Times.Once);
    }

    [Fact]
    public async Task GetPreferredChannelAsync_ReturnsPreferredChannel()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);

        var normalChannel = new ChildChannelConfig
        {
            PlatformId = "telegram",
            ChannelId = "normal",
            IsEnabled = true,
            IsPreferred = false
        };
        var preferredChannel = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "preferred",
            IsEnabled = true,
            IsPreferred = true
        };
        _manager.ConfigureChildChannel(_testChild, normalChannel);
        _manager.ConfigureChildChannel(_testChild, preferredChannel);

        // Act
        var result = await _manager.GetPreferredChannelAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("slack", result.PlatformId);
        Assert.True(result.IsPreferred);
    }

    [Fact]
    public async Task GetPreferredChannelAsync_FallsBackToFirstEnabled()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);

        // No preferred channels, just enabled ones
        var channel1 = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "channel1",
            IsEnabled = true,
            IsPreferred = false
        };
        var channel2 = new ChildChannelConfig
        {
            PlatformId = "telegram",
            ChannelId = "channel2",
            IsEnabled = false,
            IsPreferred = false
        };
        _manager.ConfigureChildChannel(_testChild, channel1);
        _manager.ConfigureChildChannel(_testChild, channel2);

        // Act
        var result = await _manager.GetPreferredChannelAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("slack", result.PlatformId);
    }

    [Fact]
    public async Task HasConfiguredChannelsAsync_ReturnsTrueWhenChannelsExist()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);

        var channel = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "test",
            IsEnabled = true
        };
        _manager.ConfigureChildChannel(_testChild, channel);

        // Act
        var result = await _manager.HasConfiguredChannelsAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasConfiguredChannelsAsync_ReturnsFalseWhenNoEnabledChannels()
    {
        // Arrange
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);

        var channel = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "test",
            IsEnabled = false
        };
        _manager.ConfigureChildChannel(_testChild, channel);

        // Act
        var result = await _manager.HasConfiguredChannelsAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ConfigureChildChannel_ReplacesExistingPlatformConfig()
    {
        // Arrange & Act
        var config1 = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "channel1",
            IsEnabled = true
        };
        var config2 = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "channel2",
            IsEnabled = false
        };
        _manager.ConfigureChildChannel(_testChild, config1);
        _manager.ConfigureChildChannel(_testChild, config2);

        // Configure for read permission
        _mockValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "channel:read"))
            .ReturnsAsync(true);

        // Assert
        var channels = _manager.GetChildChannelsAsync().Result;
        Assert.Single(channels);
        Assert.Equal("channel2", channels[0].ChannelId);
        Assert.False(channels[0].IsEnabled);
    }
}