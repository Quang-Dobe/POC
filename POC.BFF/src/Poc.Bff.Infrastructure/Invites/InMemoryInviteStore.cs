namespace Poc.Bff.Infrastructure.Invites;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Poc.Bff.Domain.Invites;

public sealed class InMemoryInviteStore : IInviteStore
{
    private readonly ConcurrentDictionary<string, InviteGrant> _grants =
        new(StringComparer.Ordinal);

    public bool TryGet(string subject, [NotNullWhen(true)] out InviteGrant? grant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return _grants.TryGetValue(subject, out grant);
    }

    public void Add(string subject, InviteGrant grant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(grant);
        _grants[subject] = grant;
    }
}
