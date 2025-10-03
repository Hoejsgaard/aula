namespace MinUddannelse.Tests.Configuration;

public class WeekLetterTests
{
    [Fact]
    public void WeekLetter_DefaultValues_AreSetCorrectly()
    {
        var weekLetter = new MinUddannelse.Configuration.WeekLetter();

        Assert.Equal(2, weekLetter.RetryIntervalHours);
        Assert.Equal(48, weekLetter.MaxRetryDurationHours);
    }

    [Fact]
    public void WeekLetter_CanSetRetryIntervalHours()
    {
        var weekLetter = new MinUddannelse.Configuration.WeekLetter
        {
            RetryIntervalHours = 4
        };

        Assert.Equal(4, weekLetter.RetryIntervalHours);
    }

    [Fact]
    public void WeekLetter_CanSetMaxRetryDurationHours()
    {
        var weekLetter = new MinUddannelse.Configuration.WeekLetter
        {
            MaxRetryDurationHours = 72
        };

        Assert.Equal(72, weekLetter.MaxRetryDurationHours);
    }

    [Fact]
    public void WeekLetter_CanSetAllProperties()
    {
        var weekLetter = new MinUddannelse.Configuration.WeekLetter
        {
            RetryIntervalHours = 3,
            MaxRetryDurationHours = 96
        };

        Assert.Equal(3, weekLetter.RetryIntervalHours);
        Assert.Equal(96, weekLetter.MaxRetryDurationHours);
    }
}
