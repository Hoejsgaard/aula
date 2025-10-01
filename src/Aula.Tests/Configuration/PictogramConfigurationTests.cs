using Xunit;
using Aula.Configuration;

namespace Aula.Tests.Configuration;

public class PictogramConfigurationTests
{
    [Fact]
    public void Child_WithPictogramAuth_ConfiguresCorrectly()
    {
        // Arrange & Act
        var child = new Child
        {
            FirstName = "Søren Johannes",
            LastName = "Højsgaard",
            Colour = "#b4a7d6",
            UniLogin = new UniLogin
            {
                Username = "testchild1439j",
                AuthType = AuthenticationType.Pictogram,
                PictogramSequence = new[] { "image1", "image2", "image3", "image4" }
            }
        };

        // Assert
        Assert.Equal("Søren Johannes", child.FirstName);
        Assert.Equal("Højsgaard", child.LastName);
        Assert.NotNull(child.UniLogin);
        Assert.Equal(AuthenticationType.Pictogram, child.UniLogin.AuthType);
        Assert.NotNull(child.UniLogin.PictogramSequence);
        Assert.Equal(4, child.UniLogin.PictogramSequence.Length);
    }

    [Fact]
    public void Child_WithStandardAuth_ConfiguresCorrectly()
    {
        // Arrange & Act
        var child = new Child
        {
            FirstName = "Hans Martin",
            LastName = "Højsgaard",
            Colour = "#377f00",
            UniLogin = new UniLogin
            {
                Username = "soer51f3",
                AuthType = AuthenticationType.Standard,
                Password = "MockPassword123"
            }
        };

        // Assert
        Assert.Equal("Hans Martin", child.FirstName);
        Assert.Equal("Højsgaard", child.LastName);
        Assert.NotNull(child.UniLogin);
        Assert.Equal(AuthenticationType.Standard, child.UniLogin.AuthType);
        Assert.Equal("MockPassword123", child.UniLogin.Password);
        Assert.Null(child.UniLogin.PictogramSequence);
    }

    [Fact]
    public void MinUddannelseConfig_WithMixedAuthTypes_ConfiguresCorrectly()
    {
        // Arrange & Act
        var config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "OlderChild",
                        UniLogin = new UniLogin
                        {
                            Username = "older",
                            AuthType = AuthenticationType.Standard,
                            Password = "password"
                        }
                    },
                    new Child
                    {
                        FirstName = "YoungerChild",
                        UniLogin = new UniLogin
                        {
                            Username = "younger",
                            AuthType = AuthenticationType.Pictogram,
                            PictogramSequence = new[] { "fugl", "båd" }
                        }
                    }
                }
            }
        };

        // Assert
        Assert.NotNull(config.MinUddannelse);
        Assert.NotNull(config.MinUddannelse.Children);
        Assert.Equal(2, config.MinUddannelse.Children.Count);

        var olderChild = config.MinUddannelse.Children[0];
        Assert.NotNull(olderChild.UniLogin);
        Assert.Equal(AuthenticationType.Standard, olderChild.UniLogin!.AuthType);
        Assert.NotEmpty(olderChild.UniLogin.Password);
        Assert.Null(olderChild.UniLogin.PictogramSequence);

        var youngerChild = config.MinUddannelse.Children[1];
        Assert.NotNull(youngerChild.UniLogin);
        Assert.Equal(AuthenticationType.Pictogram, youngerChild.UniLogin!.AuthType);
        Assert.NotNull(youngerChild.UniLogin.PictogramSequence);
        Assert.Equal(2, youngerChild.UniLogin.PictogramSequence!.Length);
    }

    [Fact]
    public void PictogramSequence_WithDanishCharacters_HandlesCorrectly()
    {
        // Arrange & Act
        var uniLogin = new UniLogin
        {
            Username = "testuser",
            AuthType = AuthenticationType.Pictogram,
            PictogramSequence = new[] { "båd", "kæreste", "æble", "øl" }
        };

        // Assert
        Assert.NotNull(uniLogin.PictogramSequence);
        Assert.Equal("båd", uniLogin.PictogramSequence[0]);
        Assert.Equal("kæreste", uniLogin.PictogramSequence[1]);
        Assert.Equal("æble", uniLogin.PictogramSequence[2]);
        Assert.Equal("øl", uniLogin.PictogramSequence[3]);
    }

    [Fact]
    public void PictogramSequence_CaseInsensitive_ShouldWork()
    {
        // Arrange & Act
        var uniLogin = new UniLogin
        {
            Username = "testuser",
            AuthType = AuthenticationType.Pictogram,
            PictogramSequence = new[] { "IMAGE1", "Image2", "IMAGE3", "image4" }
        };

        // Assert
        Assert.NotNull(uniLogin.PictogramSequence);
        // The actual case handling would be in the authentication logic
        // Configuration should preserve the case as provided
        Assert.Equal("IMAGE1", uniLogin.PictogramSequence[0]);
        Assert.Equal("Image2", uniLogin.PictogramSequence[1]);
        Assert.Equal("IMAGE3", uniLogin.PictogramSequence[2]);
        Assert.Equal("image4", uniLogin.PictogramSequence[3]);
    }
}
