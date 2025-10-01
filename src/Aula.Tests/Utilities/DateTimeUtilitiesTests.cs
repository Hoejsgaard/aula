using Aula.Utilities;

namespace Aula.Tests.Utilities;

public class DateTimeUtilitiesTests
{
    [Theory]
    [InlineData(DayOfWeek.Monday, "mandag")]
    [InlineData(DayOfWeek.Tuesday, "tirsdag")]
    [InlineData(DayOfWeek.Wednesday, "onsdag")]
    [InlineData(DayOfWeek.Thursday, "torsdag")]
    [InlineData(DayOfWeek.Friday, "fredag")]
    [InlineData(DayOfWeek.Saturday, "lørdag")]
    [InlineData(DayOfWeek.Sunday, "søndag")]
    public void GetDanishDayName_ValidDayOfWeek_ReturnsCorrectDanishName(DayOfWeek dayOfWeek, string expectedDanishName)
    {
        // Act
        var result = DateTimeUtilities.GetDanishDayName(dayOfWeek);

        // Assert
        Assert.Equal(expectedDanishName, result);
    }

    [Fact]
    public void GetDanishDayName_InvalidDayOfWeek_ReturnsUnknownDay()
    {
        // Arrange
        var invalidDayOfWeek = (DayOfWeek)999;

        // Act
        var result = DateTimeUtilities.GetDanishDayName(invalidDayOfWeek);

        // Assert
        Assert.Equal("ukendt dag", result);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Sunday)]
    public void GetDanishDayName_AllValidDaysOfWeek_ReturnsNonEmptyString(DayOfWeek dayOfWeek)
    {
        // Act
        var result = DateTimeUtilities.GetDanishDayName(dayOfWeek);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.DoesNotContain(" ", result); // Danish day names should be single words
    }

    [Fact]
    public void GetDanishDayName_AllValidDaysOfWeek_ReturnsUniqueNames()
    {
        // Arrange
        var allDaysOfWeek = Enum.GetValues<DayOfWeek>();
        var danishNames = new HashSet<string>();

        // Act & Assert
        foreach (var dayOfWeek in allDaysOfWeek)
        {
            var result = DateTimeUtilities.GetDanishDayName(dayOfWeek);
            Assert.True(danishNames.Add(result), $"Duplicate Danish name found: {result}");
        }

        // Verify we got 7 unique names
        Assert.Equal(7, danishNames.Count);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, "mandag")]
    [InlineData(DayOfWeek.Tuesday, "tirsdag")]
    [InlineData(DayOfWeek.Wednesday, "onsdag")]
    [InlineData(DayOfWeek.Thursday, "torsdag")]
    [InlineData(DayOfWeek.Friday, "fredag")]
    [InlineData(DayOfWeek.Saturday, "lørdag")]
    [InlineData(DayOfWeek.Sunday, "søndag")]
    public void GetDanishDayName_ValidDayOfWeek_ReturnsLowercaseName(DayOfWeek dayOfWeek, string expectedName)
    {
        // Act
        var result = DateTimeUtilities.GetDanishDayName(dayOfWeek);

        // Assert
        Assert.Equal(expectedName, result); // Should already be lowercase
    }
}
