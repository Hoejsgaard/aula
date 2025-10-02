namespace MinUddannelse.Configuration;

/// <summary>
/// Provides time information for configuration validation.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current date and time.
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets the current year.
    /// </summary>
    int CurrentYear { get; }
}

/// <summary>
/// System time provider implementation.
/// </summary>
public class SystemTimeProvider : ITimeProvider
{
    /// <inheritdoc />
    public DateTime Now => DateTime.Now;

    /// <inheritdoc />
    public int CurrentYear => DateTime.Now.Year;
}
