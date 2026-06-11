namespace Poc.Bff.Domain.Rbac;

using System.ComponentModel.DataAnnotations;

public sealed class RoleMap : IValidatableObject
{
    public const string SectionName = "RoleMap";

    public Dictionary<string, RoleMapEntry> Entries { get; init; } = new();

    public string[]? DefaultRoles { get; init; }

    public string? DefaultRegion { get; init; }

    public bool HasDefault =>
        DefaultRoles is { Length: > 0 } && !string.IsNullOrWhiteSpace(DefaultRegion);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasDefaultRoles = DefaultRoles is { Length: > 0 };
        var hasDefaultRegion = !string.IsNullOrWhiteSpace(DefaultRegion);

        if (hasDefaultRoles != hasDefaultRegion)
        {
            yield return new ValidationResult(
                $"'{nameof(DefaultRoles)}' and '{nameof(DefaultRegion)}' must be supplied together " +
                "or not at all; a default-reader needs both its roles and its region (the minter " +
                "throws on a blank region).",
                new[] { nameof(DefaultRoles), nameof(DefaultRegion) });
        }
    }
}
