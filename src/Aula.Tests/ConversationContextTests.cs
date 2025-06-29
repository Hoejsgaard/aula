using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Aula.Utilities;

namespace Aula.Tests;

public class ConversationContextTests
{
    [Fact]
    public void ConversationContext_DefaultValues_AreSetCorrectly()
    {
        // Act
        var context = new ConversationContext();

        // Assert
        Assert.Null(context.LastChildName);
        Assert.False(context.WasAboutToday);
        Assert.False(context.WasAboutTomorrow);
        Assert.False(context.WasAboutHomework);
        Assert.True(context.Timestamp <= DateTime.Now);
        Assert.True(context.Timestamp > DateTime.Now.AddSeconds(-1)); // Should be very recent
    }

    [Fact]
    public void ConversationContext_Properties_CanBeSet()
    {
        // Arrange
        var context = new ConversationContext();
        var testTime = DateTime.Now.AddMinutes(-5);

        // Act
        context.LastChildName = "Hans";
        context.WasAboutToday = true;
        context.WasAboutTomorrow = true;
        context.WasAboutHomework = true;
        context.Timestamp = testTime;

        // Assert
        Assert.Equal("Hans", context.LastChildName);
        Assert.True(context.WasAboutToday);
        Assert.True(context.WasAboutTomorrow);
        Assert.True(context.WasAboutHomework);
        Assert.Equal(testTime, context.Timestamp);
    }

    [Fact]
    public void IsStillValid_WithFreshTimestamp_ReturnsTrue()
    {
        // Arrange
        var context = new ConversationContext
        {
            Timestamp = DateTime.Now.AddMinutes(-5) // 5 minutes ago
        };

        // Act & Assert
        Assert.True(context.IsStillValid);
    }

    [Fact]
    public void IsStillValid_WithExactlyTenMinutesOld_ReturnsFalse()
    {
        // Arrange
        var context = new ConversationContext
        {
            Timestamp = DateTime.Now.AddMinutes(-10) // Exactly 10 minutes ago
        };

        // Act & Assert
        Assert.False(context.IsStillValid);
    }

    [Fact]
    public void IsStillValid_WithOldTimestamp_ReturnsFalse()
    {
        // Arrange
        var context = new ConversationContext
        {
            Timestamp = DateTime.Now.AddMinutes(-15) // 15 minutes ago
        };

        // Act & Assert
        Assert.False(context.IsStillValid);
    }

    [Fact]
    public void IsStillValid_WithExactlyNinePointNineMinutesOld_ReturnsTrue()
    {
        // Arrange
        var context = new ConversationContext
        {
            Timestamp = DateTime.Now.AddMinutes(-9.9) // Just under 10 minutes ago
        };

        // Act & Assert
        Assert.True(context.IsStillValid);
    }

    [Fact]
    public void ToString_WithAllPropertiesSet_ReturnsCorrectFormat()
    {
        // Arrange
        var context = new ConversationContext
        {
            LastChildName = "Søren",
            WasAboutToday = true,
            WasAboutTomorrow = false,
            WasAboutHomework = true,
            Timestamp = DateTime.Now.AddMinutes(-3.5)
        };

        // Act
        var result = context.ToString();

        // Assert
        Assert.Contains("Child: Søren", result);
        Assert.Contains("Today: True", result);
        Assert.Contains("Tomorrow: False", result);
        Assert.Contains("Homework: True", result);
        Assert.Contains("Age: 3.", result); // Should be around 3.5 minutes (using InvariantCulture)
        Assert.Contains("minutes", result);
    }

    [Fact]
    public void ToString_WithNullChildName_ReturnsNone()
    {
        // Arrange
        var context = new ConversationContext
        {
            LastChildName = null,
            Timestamp = DateTime.Now.AddMinutes(-1)
        };

        // Act
        var result = context.ToString();

        // Assert
        Assert.Contains("Child: none", result);
        Assert.Contains("Today: False", result);
        Assert.Contains("Tomorrow: False", result);
        Assert.Contains("Homework: False", result);
        Assert.Contains("Age: 1.", result); // Should be around 1.0 minutes (using InvariantCulture)
    }

    [Fact]
    public void ToString_WithEmptyChildName_ShowsEmptyString()
    {
        // Arrange
        var context = new ConversationContext
        {
            LastChildName = "",
            Timestamp = DateTime.Now.AddSeconds(-30)
        };

        // Act
        var result = context.ToString();

        // Assert
        Assert.Contains("Child: ", result);
        Assert.Contains("Age: 0.5", result); // 30 seconds = 0.5 minutes (using InvariantCulture)
    }

    [Fact]
    public void ToString_AgeCalculation_IsAccurate()
    {
        // Arrange
        var exactTime = DateTime.Now.AddMinutes(-2.5);
        var context = new ConversationContext
        {
            Timestamp = exactTime
        };

        // Act
        var result = context.ToString();

        // Assert
        // Should show approximately 2.5 minutes (with 1 decimal place) using InvariantCulture
        Assert.Contains("Age: 2.5", result);
    }
}