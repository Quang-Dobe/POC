namespace Poc.Bff.Domain.Invites;

public sealed record InviteOutcome(
    string Subject,
    bool AlreadyExisted,
    bool InvitationSent);
