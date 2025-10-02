using System;
using System.Runtime.Serialization;

namespace Aula.GoogleCalendar;

/// <summary>
/// Exception thrown when calendar event processing fails due to invalid data format.
/// </summary>
[Serializable]
public class InvalidCalendarEventException : Exception
{
    /// <summary>
    /// Initializes a new instance of the InvalidCalendarEventException class.
    /// </summary>
    public InvalidCalendarEventException()
        : base("Invalid calendar event format.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the InvalidCalendarEventException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidCalendarEventException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the InvalidCalendarEventException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InvalidCalendarEventException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the InvalidCalendarEventException class with serialized data.
    /// </summary>
    /// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
    protected InvalidCalendarEventException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
