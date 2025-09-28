using Aula.Configuration;

namespace Aula.Services;

/// <summary>
/// Rate limiting service to prevent DoS attacks and resource exhaustion.
/// Tracks operation rates per child to ensure fair resource usage.
/// </summary>
public interface IChildRateLimiter
{
    /// <summary>
    /// Checks if an operation is allowed for the specified child based on rate limits.
    /// </summary>
    /// <param name="child">The child performing the operation</param>
    /// <param name="operation">The operation identifier (e.g., "GetWeekLetter")</param>
    /// <returns>True if the operation is allowed, false if rate limit exceeded</returns>
    Task<bool> IsAllowedAsync(Child child, string operation);

    /// <summary>
    /// Records that an operation was performed by the specified child.
    /// </summary>
    /// <param name="child">The child who performed the operation</param>
    /// <param name="operation">The operation identifier</param>
    Task RecordOperationAsync(Child child, string operation);

    /// <summary>
    /// Gets the remaining allowed operations for a child within the current time window.
    /// </summary>
    /// <param name="child">The child to check</param>
    /// <param name="operation">The operation identifier</param>
    /// <returns>Number of remaining allowed operations</returns>
    Task<int> GetRemainingOperationsAsync(Child child, string operation);

    /// <summary>
    /// Resets the rate limit counters for a specific child.
    /// </summary>
    /// <param name="child">The child whose limits to reset</param>
    Task ResetLimitsAsync(Child child);
}

/// <summary>
/// Exception thrown when a rate limit is exceeded.
/// </summary>
public class RateLimitExceededException : Exception
{
    public string Operation { get; }
    public string ChildName { get; }
    public int LimitPerWindow { get; }
    public TimeSpan WindowDuration { get; }

    public RateLimitExceededException(string operation, string childName, int limitPerWindow, TimeSpan windowDuration)
        : base($"Rate limit exceeded for operation '{operation}' by {childName}. Limit: {limitPerWindow} per {windowDuration.TotalMinutes} minutes")
    {
        Operation = operation;
        ChildName = childName;
        LimitPerWindow = limitPerWindow;
        WindowDuration = windowDuration;
    }
}