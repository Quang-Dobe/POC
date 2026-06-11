namespace Poc.Bff.Domain.Rbac;

public sealed class RoleMapEntry
{
    public string[] Roles { get; init; } = Array.Empty<string>();

    public string Region { get; init; } = string.Empty;
}
