using System;
using Xunit;
using Aula.Integration;

namespace Aula.Tests.Integration;

public class AulaClientTests
{

    [Fact]
    public void Constructor_ShouldInitializeWithCorrectParameters()
    {
        // Arrange & Act
        var client = new AulaClient("testuser", "testpass");

        // Assert - Constructor should not throw and should inherit from UniLoginClient
        Assert.NotNull(client);
        Assert.IsAssignableFrom<UniLoginClient>(client);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenUsernameIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AulaClient(null!, "password"));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenPasswordIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AulaClient("username", null!));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenBothParametersAreNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AulaClient(null!, null!));
    }

    [Fact]
    public void Constructor_ShouldInitializeWithValidEmptyStrings()
    {
        // Arrange & Act
        var client = new AulaClient("", "");

        // Assert
        Assert.NotNull(client);
    }
}