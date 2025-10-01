namespace Aula.Integration.Exceptions;

public class InvalidFormDataException : AuthenticationException
{
    public InvalidFormDataException(string message) : base(message)
    {
    }
}
