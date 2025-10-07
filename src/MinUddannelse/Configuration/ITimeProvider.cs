namespace MinUddannelse.Configuration;

public interface ITimeProvider
{
    DateTime Now { get; }

    int CurrentYear { get; }
}

public class SystemTimeProvider : ITimeProvider
{
    public DateTime Now => DateTime.Now;

    public int CurrentYear => DateTime.Now.Year;
}
