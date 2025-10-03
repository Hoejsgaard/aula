using MinUddannelse.Configuration;
using Xunit;

namespace MinUddannelse.Tests.Configuration;

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
        Assert.Equal(string.Empty, child.FirstName);
        Assert.Equal(string.Empty, child.LastName);
        Assert.Equal(string.Empty, child.Colour);
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Emma")]
    [InlineData("Lucas")]
    [InlineData("Marie-Claire")]
    [InlineData("Anne-Sofie")]
    [InlineData("Hans Martin")]
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
    public void AllProperties_CanBeSetSimultaneously()
    {
        // Arrange
        var child = new Child();
        var firstName = "Emma";
        var lastName = "Doe";
        var colour = "#FF5733";

        // Act
        child.FirstName = firstName;
        child.LastName = lastName;
        child.Colour = colour;

        // Assert
        Assert.Equal(firstName, child.FirstName);
        Assert.Equal(lastName, child.LastName);
        Assert.Equal(colour, child.Colour);
    }

    [Fact]
    public void Child_HasCorrectNamespace()
    {
        // Arrange
        var type = typeof(Child);

        // Act & Assert
        Assert.Equal("MinUddannelse.Configuration", type.Namespace);
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

        // Assert
        Assert.NotNull(firstNameProperty);
        Assert.NotNull(lastNameProperty);
        Assert.NotNull(colourProperty);

        Assert.True(firstNameProperty.CanRead);
        Assert.True(firstNameProperty.CanWrite);
        Assert.Equal(typeof(string), firstNameProperty.PropertyType);

        Assert.True(lastNameProperty.CanRead);
        Assert.True(lastNameProperty.CanWrite);
        Assert.Equal(typeof(string), lastNameProperty.PropertyType);

        Assert.True(colourProperty.CanRead);
        Assert.True(colourProperty.CanWrite);
        Assert.Equal(typeof(string), colourProperty.PropertyType);
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
    }

    [Fact]
    public void Child_ObjectInitializerSyntaxWorks()
    {
        // Arrange & Act
        var child = new Child
        {
            FirstName = "Emma",
            LastName = "Doe",
            Colour = "#FF5733"
        };

        // Assert
        Assert.Equal("Emma", child.FirstName);
        Assert.Equal("Doe", child.LastName);
        Assert.Equal("#FF5733", child.Colour);
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

    [Theory]
    [InlineData("Emma", "emma")]
    [InlineData("Søren Johannes", "søren_johannes")]
    [InlineData("Hans Martin", "hans_martin")]
    [InlineData("Anne-Sofie", "anne-sofie")]
    [InlineData("UPPERCASE", "uppercase")]
    [InlineData("MixedCase Name", "mixedcase_name")]
    [InlineData("Multiple  Spaces", "multiple__spaces")]
    [InlineData("", "")]
    public void GenerateChildId_StaticMethod_TransformsCorrectly(string firstName, string expected)
    {
        // Act
        var result = Child.GenerateChildId(firstName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Emma", "emma")]
    [InlineData("Søren Johannes", "søren_johannes")]
    [InlineData("Hans Martin", "hans_martin")]
    [InlineData("Anne-Sofie", "anne-sofie")]
    [InlineData("UPPERCASE", "uppercase")]
    [InlineData("MixedCase Name", "mixedcase_name")]
    public void GetChildId_InstanceMethod_TransformsCorrectly(string firstName, string expected)
    {
        // Arrange
        var child = new Child { FirstName = firstName };

        // Act
        var result = child.GetChildId();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetChildId_UsesFirstNameProperty()
    {
        // Arrange
        var child = new Child { FirstName = "Test Child" };

        // Act
        var result = child.GetChildId();

        // Assert
        Assert.Equal("test_child", result);
        Assert.Equal(Child.GenerateChildId(child.FirstName), result);
    }

    [Fact]
    public void GenerateChildId_StaticAndInstanceMethods_ProduceSameResult()
    {
        // Arrange
        var firstName = "Test Name";
        var child = new Child { FirstName = firstName };

        // Act
        var staticResult = Child.GenerateChildId(firstName);
        var instanceResult = child.GetChildId();

        // Assert
        Assert.Equal(staticResult, instanceResult);
    }

    [Fact]
    public void GenerateChildId_HandlesDanishCharacters()
    {
        // Arrange
        var firstName = "Øystein Åse";

        // Act
        var result = Child.GenerateChildId(firstName);

        // Assert
        Assert.Equal("øystein_åse", result);
        Assert.Contains("ø", result);
        Assert.Contains("å", result);
    }

    [Fact]
    public void GenerateChildId_IsConsistentAcrossMultipleCalls()
    {
        // Arrange
        var firstName = "Consistency Test";

        // Act
        var result1 = Child.GenerateChildId(firstName);
        var result2 = Child.GenerateChildId(firstName);
        var result3 = Child.GenerateChildId(firstName);

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
        Assert.Equal("consistency_test", result1);
    }
}
