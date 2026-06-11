namespace Poc.Bff.Infrastructure.Secrets;

public sealed class SecretStoreException : Exception
{
    public SecretStoreException(string message) : base(message)
    {
    }

    public SecretStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
