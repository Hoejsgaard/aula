namespace Aula.Integration.Exceptions;

public class InvalidCalendarEventException : Exception
{
    public InvalidCalendarEventException(string message) : base(message)
    {
    }

    public InvalidCalendarEventException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
