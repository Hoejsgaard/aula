using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Configuration;

public class UniLoginTests
{
    [Fact]
    public void Constructor_InitializesEmptyStrings()
    {
        // Act
        var uniLogin = new UniLogin();

        // Assert
        Assert.NotNull(uniLogin.Username);
        Assert.NotNull(uniLogin.Password);
        Assert.Equal(string.Empty, uniLogin.Username);
        Assert.Equal(string.Empty, uniLogin.Password);
    }

    [Fact]
    public void Username_CanSetAndGetValue()
    {
        // Arrange
        var uniLogin = new UniLogin();
        var testUsername = "testuser123";

        // Act
        uniLogin.Username = testUsername;

        // Assert
        Assert.Equal(testUsername, uniLogin.Username);
    }

    [Fact]
    public void Password_CanSetAndGetValue()
    {
        // Arrange
        var uniLogin = new UniLogin();
        var testPassword = "securePassword123!";

        // Act
        uniLogin.Password = testPassword;

        // Assert
        Assert.Equal(testPassword, uniLogin.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("user@example.com")]
    [InlineData("user_with_underscores")]
    [InlineData("user-with-dashes")]
    [InlineData("user123")]
    [InlineData("UPPERCASE")]
    [InlineData("MixedCase")]
    public void Username_AcceptsVariousFormats(string username)
    {
        // Arrange
        var uniLogin = new UniLogin();

        // Act
        uniLogin.Username = username;

        // Assert
        Assert.Equal(username, uniLogin.Username);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("simple")]
    [InlineData("Complex123!@#")]
    [InlineData("very_long_password_with_many_characters_1234567890")]
    [InlineData("SpecialChars!@#$%^&*()")]
    [InlineData("Danish_æøå")]
    public void Password_AcceptsVariousFormats(string password)
    {
        // Arrange
        var uniLogin = new UniLogin();

        // Act
        uniLogin.Password = password;

        // Assert
        Assert.Equal(password, uniLogin.Password);
    }

    [Fact]
    public void Username_CanBeSetToNull()
    {
        // Arrange
        var uniLogin = new UniLogin();

        // Act
        uniLogin.Username = null!;

        // Assert
        Assert.Null(uniLogin.Username);
    }

    [Fact]
    public void Password_CanBeSetToNull()
    {
        // Arrange
        var uniLogin = new UniLogin();

        // Act
        uniLogin.Password = null!;

        // Assert
        Assert.Null(uniLogin.Password);
    }

    [Fact]
    public void BothProperties_CanBeSetSimultaneously()
    {
        // Arrange
        var uniLogin = new UniLogin();
        var username = "testuser";
        var password = "testpass";

        // Act
        uniLogin.Username = username;
        uniLogin.Password = password;

        // Assert
        Assert.Equal(username, uniLogin.Username);
        Assert.Equal(password, uniLogin.Password);
    }

    [Fact]
    public void UniLogin_HasCorrectNamespace()
    {
        // Arrange
        var type = typeof(UniLogin);

        // Act & Assert
        Assert.Equal("Aula.Configuration", type.Namespace);
    }

    [Fact]
    public void UniLogin_IsPublicClass()
    {
        // Arrange
        var type = typeof(UniLogin);

        // Act & Assert
        Assert.True(type.IsPublic);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void UniLogin_HasCorrectProperties()
    {
        // Arrange
        var type = typeof(UniLogin);

        // Act
        var usernameProperty = type.GetProperty("Username");
        var passwordProperty = type.GetProperty("Password");

        // Assert
        Assert.NotNull(usernameProperty);
        Assert.NotNull(passwordProperty);

        Assert.True(usernameProperty.CanRead);
        Assert.True(usernameProperty.CanWrite);
        Assert.Equal(typeof(string), usernameProperty.PropertyType);

        Assert.True(passwordProperty.CanRead);
        Assert.True(passwordProperty.CanWrite);
        Assert.Equal(typeof(string), passwordProperty.PropertyType);
    }

    [Fact]
    public void UniLogin_HasParameterlessConstructor()
    {
        // Arrange
        var type = typeof(UniLogin);

        // Act
        var constructor = type.GetConstructor(System.Type.EmptyTypes);

        // Assert
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void UniLogin_PropertiesAreIndependent()
    {
        // Arrange
        var uniLogin = new UniLogin();

        // Act
        uniLogin.Username = "changed_username";
        // Password should remain unchanged

        // Assert
        Assert.Equal("changed_username", uniLogin.Username);
        Assert.Equal(string.Empty, uniLogin.Password);
    }

    [Fact]
    public void UniLogin_ObjectInitializerSyntaxWorks()
    {
        // Arrange & Act
        var uniLogin = new UniLogin
        {
            Username = "inituser",
            Password = "initpass"
        };

        // Assert
        Assert.Equal("inituser", uniLogin.Username);
        Assert.Equal("initpass", uniLogin.Password);
    }
}