namespace Poc.Bff.Application.Features.Invites.CreateInvite;

public sealed record CreateInviteResponse(
    string Subject,
    bool AlreadyExisted,
    bool InvitationSent);
