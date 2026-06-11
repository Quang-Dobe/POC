namespace Poc.Bff.Domain.Rbac;

public sealed record RoleResolution(
    bool IsAllowed,
    IReadOnlyList<string> Roles,
    string Region)
{
    public static RoleResolution Denied { get; } =
        new(IsAllowed: false, Roles: Array.Empty<string>(), Region: string.Empty);

    public static RoleResolution Allowed(IReadOnlyList<string> roles, string region) =>
        new(IsAllowed: true, Roles: roles, Region: region);
}
