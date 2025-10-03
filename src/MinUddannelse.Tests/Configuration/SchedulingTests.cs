namespace MinUddannelse.Tests.Configuration;

public class SchedulingTests
{
    [Fact]
    public void Scheduling_DefaultValues_AreSetCorrectly()
    {
        var scheduling = new MinUddannelse.Configuration.Scheduling();

        Assert.Equal(10, scheduling.IntervalSeconds);
        Assert.Equal(1, scheduling.TaskExecutionWindowMinutes);
        Assert.Equal(1, scheduling.InitialOccurrenceOffsetMinutes);
    }

    [Fact]
    public void Scheduling_CanSetIntervalSeconds()
    {
        var scheduling = new MinUddannelse.Configuration.Scheduling
        {
            IntervalSeconds = 30
        };

        Assert.Equal(30, scheduling.IntervalSeconds);
    }

    [Fact]
    public void Scheduling_CanSetTaskExecutionWindowMinutes()
    {
        var scheduling = new MinUddannelse.Configuration.Scheduling
        {
            TaskExecutionWindowMinutes = 5
        };

        Assert.Equal(5, scheduling.TaskExecutionWindowMinutes);
    }

    [Fact]
    public void Scheduling_CanSetInitialOccurrenceOffsetMinutes()
    {
        var scheduling = new MinUddannelse.Configuration.Scheduling
        {
            InitialOccurrenceOffsetMinutes = 10
        };

        Assert.Equal(10, scheduling.InitialOccurrenceOffsetMinutes);
    }

    [Fact]
    public void Scheduling_CanSetAllProperties()
    {
        var scheduling = new MinUddannelse.Configuration.Scheduling
        {
            IntervalSeconds = 20,
            TaskExecutionWindowMinutes = 3,
            InitialOccurrenceOffsetMinutes = 2
        };

        Assert.Equal(20, scheduling.IntervalSeconds);
        Assert.Equal(3, scheduling.TaskExecutionWindowMinutes);
        Assert.Equal(2, scheduling.InitialOccurrenceOffsetMinutes);
    }
}
