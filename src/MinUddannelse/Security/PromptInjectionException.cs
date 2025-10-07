using System;
using System.Runtime.Serialization;

namespace MinUddannelse.Security;

/// <summary>
/// Exception thrown when prompt injection is detected.
/// </summary>
[Serializable]
public class PromptInjectionException : Exception
{
    /// <summary>
    /// Gets the name of the child for whom the prompt injection was detected.
    /// </summary>
    public string ChildName { get; }

    /// <summary>
    /// Gets the length of the attempted input (for security analysis without exposing content).
    /// </summary>
    public int InputLength { get; }

    /// <summary>
    /// Initializes a new instance of the PromptInjectionException class.
    /// </summary>
    public PromptInjectionException()
        : base("Prompt injection detected.")
    {
        ChildName = string.Empty;
        InputLength = 0;
    }

    /// <summary>
    /// Initializes a new instance of the PromptInjectionException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PromptInjectionException(string message)
        : base(message)
    {
        ChildName = string.Empty;
        InputLength = 0;
    }

    /// <summary>
    /// Initializes a new instance of the PromptInjectionException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PromptInjectionException(string message, Exception innerException)
        : base(message, innerException)
    {
        ChildName = string.Empty;
        InputLength = 0;
    }

    /// <summary>
    /// Initializes a new instance of the PromptInjectionException class with injection details.
    /// </summary>
    /// <param name="childName">The name of the child for whom injection was detected.</param>
    /// <param name="inputLength">The length of the attempted input.</param>
    public PromptInjectionException(string childName, int inputLength)
        : base($"Prompt injection detected for {childName}. Input blocked.")
    {
        ChildName = childName ?? string.Empty;
        InputLength = inputLength;
    }

    /// <summary>
    /// Initializes a new instance of the PromptInjectionException class with injection details and custom message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="childName">The name of the child for whom injection was detected.</param>
    /// <param name="inputLength">The length of the attempted input.</param>
    public PromptInjectionException(string message, string childName, int inputLength)
        : base(message)
    {
        ChildName = childName ?? string.Empty;
        InputLength = inputLength;
    }

    /// <summary>
    /// Initializes a new instance of the PromptInjectionException class with injection details and inner exception.
    /// </summary>
    /// <param name="childName">The name of the child for whom injection was detected.</param>
    /// <param name="inputLength">The length of the attempted input.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PromptInjectionException(string childName, int inputLength, Exception innerException)
        : base($"Prompt injection detected for {childName}. Input blocked.", innerException)
    {
        ChildName = childName ?? string.Empty;
        InputLength = inputLength;
    }

    /// <summary>
    /// Initializes a new instance of the PromptInjectionException class with serialized data.
    /// </summary>
    /// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    protected PromptInjectionException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ChildName = info.GetString(nameof(ChildName)) ?? string.Empty;
        InputLength = info.GetInt32(nameof(InputLength));
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
        info.AddValue(nameof(ChildName), ChildName);
        info.AddValue(nameof(InputLength), InputLength);
    }
}
