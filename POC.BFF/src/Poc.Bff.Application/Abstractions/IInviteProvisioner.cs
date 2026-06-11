namespace Poc.Bff.Application.Abstractions;

using Poc.Bff.Domain.Invites;

public interface IInviteProvisioner
{
    Task<InviteOutcome> ProvisionAsync(InviteRequest request, CancellationToken ct = default);
}
