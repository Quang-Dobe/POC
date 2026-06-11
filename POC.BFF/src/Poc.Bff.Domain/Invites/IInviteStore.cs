namespace Poc.Bff.Domain.Invites;

using System.Diagnostics.CodeAnalysis;

public interface IInviteStore
{
    bool TryGet(string subject, [NotNullWhen(true)] out InviteGrant? grant);

    void Add(string subject, InviteGrant grant);
}
