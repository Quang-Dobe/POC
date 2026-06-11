namespace Poc.Bff.Infrastructure.Invites;

using System.Security.Cryptography;
using System.Text;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Domain.Invites;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class EntraInviteProvisioner : IInviteProvisioner
{
    private readonly ILogger<EntraInviteProvisioner> _logger;
    private readonly string _frontendReturnUrl;

    public EntraInviteProvisioner(
        IOptions<AuthOptions> authOptions,
        ILogger<EntraInviteProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(authOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _frontendReturnUrl = authOptions.Value.FrontendReturnUrl;
    }

    public async Task<InviteOutcome> ProvisionAsync(InviteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Username);

        var invitation = new GraphInvitation(
            InvitedUserEmailAddress: request.Username,
            InviteRedirectUrl: _frontendReturnUrl,
            SendInvitationMessage: true);

        return await SendGraphInvitationAsync(invitation, ct).ConfigureAwait(false);
    }

    private Task<InviteOutcome> SendGraphInvitationAsync(GraphInvitation invitation, CancellationToken ct)
    {
        var inviteId = DeterministicInviteId(invitation.InvitedUserEmailAddress);
        var outcome = new InviteOutcome(
            Subject: invitation.InvitedUserEmailAddress,
            RedeemUrl: $"https://invite.stub/redeem/{inviteId}",
            AlreadyExisted: false);

        _logger.LogInformation("Entra invite (stub): built a deterministic guest invitation outcome.");
        return Task.FromResult(outcome);
    }

    private static string DeterministicInviteId(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return new Guid(bytes.AsSpan(0, 16).ToArray()).ToString("N");
    }

    private sealed record GraphInvitation(
        string InvitedUserEmailAddress,
        string InviteRedirectUrl,
        bool SendInvitationMessage);
}
