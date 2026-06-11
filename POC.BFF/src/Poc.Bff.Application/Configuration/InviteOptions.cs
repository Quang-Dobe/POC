namespace Poc.Bff.Application.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class InviteOptions : IValidatableObject
{
    public const string SectionName = "Invite";

    public string[]? DefaultRoles { get; init; }

    public string? DefaultRegion { get; init; }

    [Required]
    public string TenantId { get; init; } = default!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasDefaultRoles = DefaultRoles is { Length: > 0 };
        var hasDefaultRegion = !string.IsNullOrWhiteSpace(DefaultRegion);

        if (hasDefaultRoles != hasDefaultRegion)
        {
            yield return new ValidationResult(
                $"'{nameof(DefaultRoles)}' and '{nameof(DefaultRegion)}' must be supplied together " +
                "or not at all; an invite grant needs both its roles and its region (the minter " +
                "throws on a blank region).",
                new[] { nameof(DefaultRoles), nameof(DefaultRegion) });
        }
    }
}
