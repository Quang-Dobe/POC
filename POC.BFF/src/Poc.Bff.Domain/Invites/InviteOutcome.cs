namespace Poc.Bff.Domain.Invites;

public sealed record InviteOutcome(
    string Subject,
    string RedeemUrl,
    bool AlreadyExisted,
    string GeneratedPassword);
