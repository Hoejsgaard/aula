using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Configuration;

public class ChildTests
{
    [Fact]
    public void Constructor_InitializesEmptyStrings()
    {
        // Act
        var child = new Child();

        // Assert
        Assert.NotNull(child.FirstName);
        Assert.NotNull(child.LastName);
        Assert.NotNull(child.Colour);
        Assert.NotNull(child.GoogleCalendarId);
        Assert.Equal(string.Empty, child.FirstName);
        Assert.Equal(string.Empty, child.LastName);
        Assert.Equal(string.Empty, child.Colour);
        Assert.Equal(string.Empty, child.GoogleCalendarId);
    }

    [Fact]
    public void FirstName_CanSetAndGetValue()
    {
        // Arrange
        var child = new Child();
        var testFirstName = "Emma";

        // Act
        child.FirstName = testFirstName;

        // Assert
        Assert.Equal(testFirstName, child.FirstName);
    }

    [Fact]
    public void LastName_CanSetAndGetValue()
    {
        // Arrange
        var child = new Child();
        var testLastName = "Doe";

        // Act
        child.LastName = testLastName;

        // Assert
        Assert.Equal(testLastName, child.LastName);
    }

    [Fact]
    public void Colour_CanSetAndGetValue()
    {
        // Arrange
        var child = new Child();
        var testColour = "#FF5733";

        // Act
        child.Colour = testColour;

        // Assert
        Assert.Equal(testColour, child.Colour);
    }

    [Fact]
    public void GoogleCalendarId_CanSetAndGetValue()
    {
        // Arrange
        var child = new Child();
        var testCalendarId = "primary";

        // Act
        child.GoogleCalendarId = testCalendarId;

        // Assert
        Assert.Equal(testCalendarId, child.GoogleCalendarId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Emma")]
    [InlineData("Lucas")]
    [InlineData("Marie-Claire")]
    [InlineData("Anne-Sofie")]
    [InlineData("Søren")]
    [InlineData("Åse")]
    [InlineData("VeryLongFirstNameWithManyCharacters")]
    public void FirstName_AcceptsVariousFormats(string firstName)
    {
        // Arrange
        var child = new Child();

        // Act
        child.FirstName = firstName;

        // Assert
        Assert.Equal(firstName, child.FirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Doe")]
    [InlineData("Smith")]
    [InlineData("van der Berg")]
    [InlineData("O'Connor")]
    [InlineData("Müller")]
    [InlineData("García")]
    [InlineData("Æbelholt")]
    [InlineData("VeryLongLastNameWithManyCharacters")]
    public void LastName_AcceptsVariousFormats(string lastName)
    {
        // Arrange
        var child = new Child();

        // Act
        child.LastName = lastName;

        // Assert
        Assert.Equal(lastName, child.LastName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#FF5733")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("red")]
    [InlineData("blue")]
    [InlineData("rgb(255, 87, 51)")]
    [InlineData("rgba(255, 87, 51, 0.5)")]
    [InlineData("hsl(11, 100%, 60%)")]
    public void Colour_AcceptsVariousFormats(string colour)
    {
        // Arrange
        var child = new Child();

        // Act
        child.Colour = colour;

        // Assert
        Assert.Equal(colour, child.Colour);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("primary")]
    [InlineData("emma@example.com")]
    [InlineData("calendar-id-123")]
    [InlineData("very.long.calendar.id.with.many.dots")]
    [InlineData("calendar_with_underscores")]
    [InlineData("UPPERCASE_CALENDAR_ID")]
    public void GoogleCalendarId_AcceptsVariousFormats(string calendarId)
    {
        // Arrange
        var child = new Child();

        // Act
        child.GoogleCalendarId = calendarId;

        // Assert
        Assert.Equal(calendarId, child.GoogleCalendarId);
    }

    [Fact]
    public void FirstName_CanBeSetToNull()
    {
        // Arrange
        var child = new Child();

        // Act
        child.FirstName = null!;

        // Assert
        Assert.Null(child.FirstName);
    }

    [Fact]
    public void LastName_CanBeSetToNull()
    {
        // Arrange
        var child = new Child();

        // Act
        child.LastName = null!;

        // Assert
        Assert.Null(child.LastName);
    }

    [Fact]
    public void Colour_CanBeSetToNull()
    {
        // Arrange
        var child = new Child();

        // Act
        child.Colour = null!;

        // Assert
        Assert.Null(child.Colour);
    }

    [Fact]
    public void GoogleCalendarId_CanBeSetToNull()
    {
        // Arrange
        var child = new Child();

        // Act
        child.GoogleCalendarId = null!;

        // Assert
        Assert.Null(child.GoogleCalendarId);
    }

    [Fact]
    public void AllProperties_CanBeSetSimultaneously()
    {
        // Arrange
        var child = new Child();
        var firstName = "Emma";
        var lastName = "Doe";
        var colour = "#FF5733";
        var calendarId = "emma@example.com";

        // Act
        child.FirstName = firstName;
        child.LastName = lastName;
        child.Colour = colour;
        child.GoogleCalendarId = calendarId;

        // Assert
        Assert.Equal(firstName, child.FirstName);
        Assert.Equal(lastName, child.LastName);
        Assert.Equal(colour, child.Colour);
        Assert.Equal(calendarId, child.GoogleCalendarId);
    }

    [Fact]
    public void Child_HasCorrectNamespace()
    {
        // Arrange
        var type = typeof(Child);

        // Act & Assert
        Assert.Equal("Aula.Configuration", type.Namespace);
    }

    [Fact]
    public void Child_IsPublicClass()
    {
        // Arrange
        var type = typeof(Child);

        // Act & Assert
        Assert.True(type.IsPublic);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void Child_HasCorrectProperties()
    {
        // Arrange
        var type = typeof(Child);

        // Act
        var firstNameProperty = type.GetProperty("FirstName");
        var lastNameProperty = type.GetProperty("LastName");
        var colourProperty = type.GetProperty("Colour");
        var googleCalendarIdProperty = type.GetProperty("GoogleCalendarId");

        // Assert
        Assert.NotNull(firstNameProperty);
        Assert.NotNull(lastNameProperty);
        Assert.NotNull(colourProperty);
        Assert.NotNull(googleCalendarIdProperty);
        
        Assert.True(firstNameProperty.CanRead);
        Assert.True(firstNameProperty.CanWrite);
        Assert.Equal(typeof(string), firstNameProperty.PropertyType);
        
        Assert.True(lastNameProperty.CanRead);
        Assert.True(lastNameProperty.CanWrite);
        Assert.Equal(typeof(string), lastNameProperty.PropertyType);
        
        Assert.True(colourProperty.CanRead);
        Assert.True(colourProperty.CanWrite);
        Assert.Equal(typeof(string), colourProperty.PropertyType);
        
        Assert.True(googleCalendarIdProperty.CanRead);
        Assert.True(googleCalendarIdProperty.CanWrite);
        Assert.Equal(typeof(string), googleCalendarIdProperty.PropertyType);
    }

    [Fact]
    public void Child_HasParameterlessConstructor()
    {
        // Arrange
        var type = typeof(Child);

        // Act
        var constructor = type.GetConstructor(System.Type.EmptyTypes);

        // Assert
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void Child_PropertiesAreIndependent()
    {
        // Arrange
        var child = new Child();

        // Act
        child.FirstName = "Changed";
        // Other properties should remain unchanged

        // Assert
        Assert.Equal("Changed", child.FirstName);
        Assert.Equal(string.Empty, child.LastName);
        Assert.Equal(string.Empty, child.Colour);
        Assert.Equal(string.Empty, child.GoogleCalendarId);
    }

    [Fact]
    public void Child_ObjectInitializerSyntaxWorks()
    {
        // Arrange & Act
        var child = new Child
        {
            FirstName = "Emma",
            LastName = "Doe",
            Colour = "#FF5733",
            GoogleCalendarId = "emma@example.com"
        };

        // Assert
        Assert.Equal("Emma", child.FirstName);
        Assert.Equal("Doe", child.LastName);
        Assert.Equal("#FF5733", child.Colour);
        Assert.Equal("emma@example.com", child.GoogleCalendarId);
    }

    [Fact]
    public void Child_SupportsCommonDanishNames()
    {
        // Arrange
        var child = new Child();

        // Act
        child.FirstName = "Søren";
        child.LastName = "Ørsted";

        // Assert
        Assert.Equal("Søren", child.FirstName);
        Assert.Equal("Ørsted", child.LastName);
        Assert.Contains("ø", child.FirstName);
        Assert.Contains("Ø", child.LastName);
    }

    [Fact]
    public void Child_SupportsCommonColorFormats()
    {
        // Arrange
        var child = new Child();

        // Act & Assert - Hex format
        child.Colour = "#FF5733";
        Assert.Equal("#FF5733", child.Colour);

        // Act & Assert - Named color
        child.Colour = "red";
        Assert.Equal("red", child.Colour);

        // Act & Assert - RGB format
        child.Colour = "rgb(255, 87, 51)";
        Assert.Equal("rgb(255, 87, 51)", child.Colour);
    }
}