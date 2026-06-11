namespace Poc.Bff.Infrastructure.Rbac;

using Poc.Bff.Domain.Invites;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Domain.Rbac;
using Microsoft.Extensions.Logging;

public sealed class InviteAwareRoleResolver : IRoleResolver
{
    private readonly ConfigRoleResolver _inner;
    private readonly IInviteStore _store;
    private readonly ILogger<InviteAwareRoleResolver> _logger;

    public InviteAwareRoleResolver(
        ConfigRoleResolver inner,
        IInviteStore store,
        ILogger<InviteAwareRoleResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _store = store;
        _logger = logger;
    }

    public RoleResolution Resolve(string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var inner = _inner.Resolve(subject);
        if (inner.IsAllowed)
        {
            return inner;
        }

        if (_store.TryGet(subject, out var grant))
        {
            _logger.LogInformation("Resolved an invited subject to its default grant.");
            return RoleResolution.Allowed(grant.Roles, grant.Region);
        }

        return RoleResolution.Denied;
    }
}
