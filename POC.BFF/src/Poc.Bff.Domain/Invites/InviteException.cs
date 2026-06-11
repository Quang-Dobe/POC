namespace Poc.Bff.Domain.Invites;

public sealed class InviteException : Exception
{
    public InviteException(string message)
        : base(message)
    {
    }

    public InviteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
