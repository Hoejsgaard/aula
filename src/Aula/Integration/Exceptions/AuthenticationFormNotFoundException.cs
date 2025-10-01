namespace Aula.Integration.Exceptions;

public class AuthenticationFormNotFoundException : AuthenticationException
{
    public AuthenticationFormNotFoundException(string message) : base(message)
    {
    }
}
