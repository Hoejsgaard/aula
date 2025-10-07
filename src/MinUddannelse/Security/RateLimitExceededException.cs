using System;
using System.Runtime.Serialization;

namespace MinUddannelse.Security;

/// <summary>
/// Exception thrown when a rate limit is exceeded.
/// </summary>
[Serializable]
public class RateLimitExceededException : Exception
{
    public string Operation { get; }
    public string ChildName { get; }
    public int LimitPerWindow { get; }
    public TimeSpan WindowDuration { get; }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class.
    /// </summary>
    public RateLimitExceededException()
        : base("Rate limit exceeded.")
    {
        Operation = string.Empty;
        ChildName = string.Empty;
        LimitPerWindow = 0;
        WindowDuration = TimeSpan.Zero;
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RateLimitExceededException(string message)
        : base(message)
    {
        Operation = string.Empty;
        ChildName = string.Empty;
        LimitPerWindow = 0;
        WindowDuration = TimeSpan.Zero;
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public RateLimitExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
        Operation = string.Empty;
        ChildName = string.Empty;
        LimitPerWindow = 0;
        WindowDuration = TimeSpan.Zero;
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with rate limit details.
    /// </summary>
    /// <param name="operation">The operation that exceeded the rate limit.</param>
    /// <param name="childName">The name of the child who exceeded the rate limit.</param>
    /// <param name="limitPerWindow">The maximum number of operations allowed per window.</param>
    /// <param name="windowDuration">The duration of the rate limiting window.</param>
    public RateLimitExceededException(string operation, string childName, int limitPerWindow, TimeSpan windowDuration)
        : base($"Rate limit exceeded for operation '{operation}' by {childName}. Limit: {limitPerWindow} per {windowDuration.TotalMinutes} minutes")
    {
        Operation = operation ?? string.Empty;
        ChildName = childName ?? string.Empty;
        LimitPerWindow = limitPerWindow;
        WindowDuration = windowDuration;
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with rate limit details and inner exception.
    /// </summary>
    /// <param name="operation">The operation that exceeded the rate limit.</param>
    /// <param name="childName">The name of the child who exceeded the rate limit.</param>
    /// <param name="limitPerWindow">The maximum number of operations allowed per window.</param>
    /// <param name="windowDuration">The duration of the rate limiting window.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public RateLimitExceededException(string operation, string childName, int limitPerWindow, TimeSpan windowDuration, Exception innerException)
        : base($"Rate limit exceeded for operation '{operation}' by {childName}. Limit: {limitPerWindow} per {windowDuration.TotalMinutes} minutes", innerException)
    {
        Operation = operation ?? string.Empty;
        ChildName = childName ?? string.Empty;
        LimitPerWindow = limitPerWindow;
        WindowDuration = windowDuration;
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with serialized data.
    /// </summary>
    /// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    protected RateLimitExceededException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Operation = info.GetString(nameof(Operation)) ?? string.Empty;
        ChildName = info.GetString(nameof(ChildName)) ?? string.Empty;
        LimitPerWindow = info.GetInt32(nameof(LimitPerWindow));
        WindowDuration = TimeSpan.FromTicks(info.GetInt64(nameof(WindowDuration)));
    }

    /// <summary>
    /// Sets the SerializationInfo with information about the exception.
    /// </summary>
    /// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Operation), Operation);
        info.AddValue(nameof(ChildName), ChildName);
        info.AddValue(nameof(LimitPerWindow), LimitPerWindow);
        info.AddValue(nameof(WindowDuration), WindowDuration.Ticks);
    }
}
