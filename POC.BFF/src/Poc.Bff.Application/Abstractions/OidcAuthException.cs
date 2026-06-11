namespace Poc.Bff.Application.Abstractions;

public sealed class OidcAuthException : Exception
{
    public OidcAuthException(string message)
        : base(message)
    {
    }

    public OidcAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
