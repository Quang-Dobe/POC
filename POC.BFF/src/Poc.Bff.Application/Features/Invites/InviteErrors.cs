using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Invites;

public static class InviteErrors
{
    public static readonly Error ProvisioningFailed = new(
        Code: "Invites.ProvisioningFailed",
        Message: "The upstream identity provider failed to provision the invited user.",
        Type: ErrorType.Upstream);
}
