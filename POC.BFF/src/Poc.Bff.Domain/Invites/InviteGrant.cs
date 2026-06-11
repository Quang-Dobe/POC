namespace Poc.Bff.Domain.Invites;

public sealed record InviteGrant(
    IReadOnlyList<string> Roles,
    string Region)
{
    public static InviteGrant Create(IReadOnlyList<string> roles, string region)
    {
        if (roles is not { Count: > 0 } || roles.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "An invite grant requires at least one non-blank role.", nameof(roles));
        }

        if (string.IsNullOrWhiteSpace(region))
        {
            throw new ArgumentException(
                "An invite grant requires a non-blank region; the downstream minter binds to it.",
                nameof(region));
        }

        return new InviteGrant(roles, region);
    }
}
