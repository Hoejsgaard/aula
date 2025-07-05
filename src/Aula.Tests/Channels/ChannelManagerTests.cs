using Microsoft.Extensions.Logging;
using Moq;
using Aula.Channels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests.Channels;

public class ChannelManagerTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger<ChannelManager>> _mockLogger;
    private readonly ChannelManager _channelManager;
    private readonly Mock<IChannel> _mockChannel1;
    private readonly Mock<IChannel> _mockChannel2;

    public ChannelManagerTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger<ChannelManager>>();
        _mockChannel1 = new Mock<IChannel>();
        _mockChannel2 = new Mock<IChannel>();

        // Use real LoggerFactory instead of mocking extension method
        var loggerFactory = new LoggerFactory();

        _mockChannel1.Setup(c => c.PlatformId).Returns("slack");
        _mockChannel1.Setup(c => c.IsEnabled).Returns(true);
        _mockChannel1.Setup(c => c.FormatMessage(It.IsAny<string>(), It.IsAny<MessageFormat>())).Returns<string, MessageFormat>((msg, fmt) => msg);
        
        _mockChannel2.Setup(c => c.PlatformId).Returns("telegram");
        _mockChannel2.Setup(c => c.IsEnabled).Returns(false);
        _mockChannel2.Setup(c => c.FormatMessage(It.IsAny<string>(), It.IsAny<MessageFormat>())).Returns<string, MessageFormat>((msg, fmt) => msg);

        _channelManager = new ChannelManager(loggerFactory);
    }

    [Fact]
    public void Constructor_WithValidLoggerFactory_InitializesCorrectly()
    {
        // Arrange & Act
        var loggerFactory = new LoggerFactory();
        var manager = new ChannelManager(loggerFactory);

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ChannelManager(null!));
    }

    [Fact]
    public void GetAllChannels_WithNoChannels_ReturnsEmptyList()
    {
        // Arrange & Act
        var channels = _channelManager.GetAllChannels();

        // Assert
        Assert.NotNull(channels);
        Assert.Empty(channels);
    }

    [Fact]
    public void GetAllChannels_WithRegisteredChannels_ReturnsAllChannels()
    {
        // Arrange
        _channelManager.RegisterChannel(_mockChannel1.Object);
        _channelManager.RegisterChannel(_mockChannel2.Object);

        // Act
        var channels = _channelManager.GetAllChannels();

        // Assert
        Assert.Equal(2, channels.Count);
        Assert.Contains(_mockChannel1.Object, channels);
        Assert.Contains(_mockChannel2.Object, channels);
    }

    [Fact]
    public void GetEnabledChannels_WithNoChannels_ReturnsEmptyList()
    {
        // Arrange & Act
        var channels = _channelManager.GetEnabledChannels();

        // Assert
        Assert.NotNull(channels);
        Assert.Empty(channels);
    }

    [Fact]
    public void GetEnabledChannels_WithMixedChannels_ReturnsOnlyEnabledChannels()
    {
        // Arrange
        _channelManager.RegisterChannel(_mockChannel1.Object); // enabled
        _channelManager.RegisterChannel(_mockChannel2.Object); // disabled

        // Act
        var channels = _channelManager.GetEnabledChannels();

        // Assert
        Assert.Single(channels);
        Assert.Contains(_mockChannel1.Object, channels);
        Assert.DoesNotContain(_mockChannel2.Object, channels);
    }

    [Fact]
    public void GetChannel_WithExistingPlatformId_ReturnsChannel()
    {
        // Arrange
        _channelManager.RegisterChannel(_mockChannel1.Object);

        // Act
        var channel = _channelManager.GetChannel("slack");

        // Assert
        Assert.Equal(_mockChannel1.Object, channel);
    }

    [Fact]
    public void GetChannel_WithNonExistentPlatformId_ReturnsNull()
    {
        // Arrange & Act
        var channel = _channelManager.GetChannel("nonexistent");

        // Assert
        Assert.Null(channel);
    }

    [Fact]
    public void RegisterChannel_WithValidChannel_AddsChannel()
    {
        // Arrange & Act
        _channelManager.RegisterChannel(_mockChannel1.Object);

        // Assert
        var channels = _channelManager.GetAllChannels();
        Assert.Single(channels);
        Assert.Contains(_mockChannel1.Object, channels);
    }

    [Fact]
    public void RegisterChannel_WithNullChannel_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => _channelManager.RegisterChannel(null!));
    }

    [Fact]
    public void RegisterChannel_WithDuplicatePlatformId_ReplacesExistingChannel()
    {
        // Arrange
        var mockNewChannel = new Mock<IChannel>();
        mockNewChannel.Setup(c => c.PlatformId).Returns("slack");
        mockNewChannel.Setup(c => c.IsEnabled).Returns(true);

        _channelManager.RegisterChannel(_mockChannel1.Object);

        // Act
        _channelManager.RegisterChannel(mockNewChannel.Object);

        // Assert
        var channels = _channelManager.GetAllChannels();
        Assert.Single(channels);
        Assert.Contains(mockNewChannel.Object, channels);
        Assert.DoesNotContain(_mockChannel1.Object, channels);
        
        // Note: Can't verify logging with real LoggerFactory in this test setup
    }

    [Fact]
    public async Task BroadcastMessageAsync_WithNoChannels_CompletesWithoutError()
    {
        // Arrange
        var message = "test message";

        // Act & Assert - Should not throw
        await _channelManager.BroadcastMessageAsync(message);
    }

    [Fact]
    public async Task BroadcastMessageAsync_WithEnabledChannels_SendsToAllEnabledChannels()
    {
        // Arrange
        var message = "test message";
        _channelManager.RegisterChannel(_mockChannel1.Object); // enabled
        _channelManager.RegisterChannel(_mockChannel2.Object); // disabled

        // Act
        await _channelManager.BroadcastMessageAsync(message);

        // Assert
        _mockChannel1.Verify(c => c.SendMessageAsync(message), Times.Once);
        _mockChannel2.Verify(c => c.SendMessageAsync(message), Times.Never);
    }

    [Fact]
    public async Task SendToChannelsAsync_WithValidPlatformIds_SendsOnlyToEnabledChannels()
    {
        // Arrange
        var message = "test message";
        _channelManager.RegisterChannel(_mockChannel1.Object); // enabled
        _channelManager.RegisterChannel(_mockChannel2.Object); // disabled

        // Act
        await _channelManager.SendToChannelsAsync(message, "slack", "telegram");

        // Assert
        _mockChannel1.Verify(c => c.SendMessageAsync(message), Times.Once);
        _mockChannel2.Verify(c => c.SendMessageAsync(message), Times.Never); // disabled channel should not receive message
    }

    [Fact]
    public async Task SendToChannelsAsync_WithNullMessage_LogsWarningAndReturns()
    {
        // Arrange
        _channelManager.RegisterChannel(_mockChannel1.Object);

        // Act - Should not throw, just log warning and return
        await _channelManager.SendToChannelsAsync(null!, "slack");

        // Assert - Channel should not be called
        _mockChannel1.Verify(c => c.SendMessageAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendToChannelsAsync_WithNonExistentPlatformId_SkipsUnknownChannels()
    {
        // Arrange
        var message = "test message";
        _channelManager.RegisterChannel(_mockChannel1.Object);

        // Act - Should not throw even with non-existent platform ID
        await _channelManager.SendToChannelsAsync(message, "slack", "nonexistent");

        // Assert
        _mockChannel1.Verify(c => c.SendMessageAsync(message), Times.Once);
    }

    [Fact]
    public async Task InitializeAllChannelsAsync_WithRegisteredChannels_InitializesAllChannels()
    {
        // Arrange
        _channelManager.RegisterChannel(_mockChannel1.Object);
        _channelManager.RegisterChannel(_mockChannel2.Object);

        // Act
        await _channelManager.InitializeAllChannelsAsync();

        // Assert
        _mockChannel1.Verify(c => c.InitializeAsync(), Times.Once);
        _mockChannel2.Verify(c => c.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task StartAllChannelsAsync_WithRegisteredChannels_StartsOnlyEnabledChannels()
    {
        // Arrange
        _channelManager.RegisterChannel(_mockChannel1.Object); // enabled
        _channelManager.RegisterChannel(_mockChannel2.Object); // disabled

        // Act
        await _channelManager.StartAllChannelsAsync();

        // Assert
        _mockChannel1.Verify(c => c.StartAsync(), Times.Once);
        _mockChannel2.Verify(c => c.StartAsync(), Times.Never); // disabled channel should not be started
    }

    [Fact]
    public async Task StopAllChannelsAsync_WithRegisteredChannels_StopsAllChannels()
    {
        // Arrange
        _channelManager.RegisterChannel(_mockChannel1.Object);
        _channelManager.RegisterChannel(_mockChannel2.Object);

        // Act
        await _channelManager.StopAllChannelsAsync();

        // Assert
        _mockChannel1.Verify(c => c.StopAsync(), Times.Once);
        _mockChannel2.Verify(c => c.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task TestAllChannelsAsync_WithRegisteredChannels_TestsAllChannels()
    {
        // Arrange
        _mockChannel1.Setup(c => c.TestConnectionAsync()).ReturnsAsync(true);
        _mockChannel2.Setup(c => c.TestConnectionAsync()).ReturnsAsync(false);
        
        _channelManager.RegisterChannel(_mockChannel1.Object);
        _channelManager.RegisterChannel(_mockChannel2.Object);

        // Act
        var results = await _channelManager.TestAllChannelsAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.True(results["slack"]);
        Assert.False(results["telegram"]);
        
        _mockChannel1.Verify(c => c.TestConnectionAsync(), Times.Once);
        _mockChannel2.Verify(c => c.TestConnectionAsync(), Times.Once);
    }

    [Fact]
    public void ChannelManager_ImplementsIChannelManagerInterface()
    {
        // Arrange & Act & Assert
        Assert.True(typeof(IChannelManager).IsAssignableFrom(typeof(ChannelManager)));
    }

    [Fact]
    public void ChannelManager_HasCorrectNamespace()
    {
        // Arrange
        var managerType = typeof(ChannelManager);

        // Act & Assert
        Assert.Equal("Aula.Channels", managerType.Namespace);
    }

    [Fact]
    public void ChannelManager_IsPublicClass()
    {
        // Arrange
        var managerType = typeof(ChannelManager);

        // Act & Assert
        Assert.True(managerType.IsPublic);
        Assert.False(managerType.IsAbstract);
        Assert.False(managerType.IsSealed);
    }

    [Fact]
    public void ChannelManager_HasCorrectPublicMethods()
    {
        // Arrange
        var managerType = typeof(ChannelManager);

        // Act & Assert
        Assert.NotNull(managerType.GetMethod("GetAllChannels"));
        Assert.NotNull(managerType.GetMethod("GetEnabledChannels"));
        Assert.NotNull(managerType.GetMethod("GetChannel"));
        Assert.NotNull(managerType.GetMethod("RegisterChannel"));
        Assert.NotNull(managerType.GetMethod("BroadcastMessageAsync"));
        Assert.NotNull(managerType.GetMethod("SendToChannelsAsync"));
        Assert.NotNull(managerType.GetMethod("InitializeAllChannelsAsync"));
        Assert.NotNull(managerType.GetMethod("StartAllChannelsAsync"));
        Assert.NotNull(managerType.GetMethod("StopAllChannelsAsync"));
        Assert.NotNull(managerType.GetMethod("TestAllChannelsAsync"));
    }

    [Fact]
    public void ChannelManager_ConstructorParametersHaveCorrectTypes()
    {
        // Arrange
        var managerType = typeof(ChannelManager);
        var constructor = managerType.GetConstructors()[0];

        // Act
        var parameters = constructor.GetParameters();

        // Assert
        Assert.Single(parameters);
        Assert.Equal(typeof(ILoggerFactory), parameters[0].ParameterType);
    }

    [Fact]
    public void ChannelManager_GetAllChannels_ReturnsReadOnlyList()
    {
        // Arrange & Act
        var channels = _channelManager.GetAllChannels();

        // Assert
        Assert.IsAssignableFrom<IReadOnlyList<IChannel>>(channels);
    }

    [Fact]
    public void ChannelManager_GetEnabledChannels_ReturnsReadOnlyList()
    {
        // Arrange & Act
        var channels = _channelManager.GetEnabledChannels();

        // Assert
        Assert.IsAssignableFrom<IReadOnlyList<IChannel>>(channels);
    }

    [Fact]
    public void RegisterChannel_MultipleChannels_MaintainsAllRegistrations()
    {
        // Arrange
        var mockChannel3 = new Mock<IChannel>();
        mockChannel3.Setup(c => c.PlatformId).Returns("discord");
        mockChannel3.Setup(c => c.IsEnabled).Returns(true);

        // Act
        _channelManager.RegisterChannel(_mockChannel1.Object);
        _channelManager.RegisterChannel(_mockChannel2.Object);
        _channelManager.RegisterChannel(mockChannel3.Object);

        // Assert
        var allChannels = _channelManager.GetAllChannels();
        Assert.Equal(3, allChannels.Count);
        
        Assert.NotNull(_channelManager.GetChannel("slack"));
        Assert.NotNull(_channelManager.GetChannel("telegram"));
        Assert.NotNull(_channelManager.GetChannel("discord"));
    }

    [Fact]
    public async Task ChannelManager_AsyncOperations_HandleEmptyChannelList()
    {
        // Arrange & Act & Assert - Should not throw
        await _channelManager.BroadcastMessageAsync("test");
        await _channelManager.InitializeAllChannelsAsync();
        await _channelManager.StartAllChannelsAsync();
        await _channelManager.StopAllChannelsAsync();
        
        var testResults = await _channelManager.TestAllChannelsAsync();
        Assert.Empty(testResults);
    }
}