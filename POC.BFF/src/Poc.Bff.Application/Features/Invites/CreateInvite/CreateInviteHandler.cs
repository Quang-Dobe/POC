using Microsoft.Extensions.Options;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Domain.Invites;

namespace Poc.Bff.Application.Features.Invites.CreateInvite;

public sealed class CreateInviteHandler
    : IRequestHandler<CreateInviteCommand, Result<CreateInviteResponse>>
{
    private readonly IInviteProvisioner _provisioner;
    private readonly IInviteStore _store;
    private readonly IOptions<InviteOptions> _options;

    public CreateInviteHandler(
        IInviteProvisioner provisioner,
        IInviteStore store,
        IOptions<InviteOptions> options)
    {
        ArgumentNullException.ThrowIfNull(provisioner);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        _provisioner = provisioner;
        _store = store;
        _options = options;
    }

    public async Task<Result<CreateInviteResponse>> Handle(
        CreateInviteCommand request,
        CancellationToken cancellationToken)
    {
        var inviteRequest = new InviteRequest(
            Username: request.Username,
            DisplayName: request.DisplayName);

        InviteOutcome outcome;
        try
        {
            outcome = await _provisioner.ProvisionAsync(inviteRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InviteException)
        {
            return Result.Failure<CreateInviteResponse>(InviteErrors.ProvisioningFailed);
        }

        var grant = InviteGrant.Create(
            _options.Value.DefaultRoles!,
            _options.Value.DefaultRegion!);

        _store.Add(outcome.Subject, grant);

        return Result.Success(new CreateInviteResponse(
            Subject: outcome.Subject,
            RedeemUrl: outcome.RedeemUrl,
            AlreadyExisted: outcome.AlreadyExisted));
    }
}
