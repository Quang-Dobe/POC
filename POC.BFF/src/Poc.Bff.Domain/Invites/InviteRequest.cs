namespace Poc.Bff.Domain.Invites;

public sealed record InviteRequest(
    string Username,
    string? DisplayName = null);
