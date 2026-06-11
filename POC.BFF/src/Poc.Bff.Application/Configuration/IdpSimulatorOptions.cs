namespace Poc.Bff.Application.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class IdpSimulatorOptions : IValidatableObject
{
    public const string SectionName = "IdpSimulator";

    [Required]
    public string Issuer { get; init; } = default!;

    [Required]
    public string Audience { get; init; } = default!;

    [Range(1, int.MaxValue)]
    public int DownstreamTokenTtlSeconds { get; init; }

    [Required]
    public string SigningKeyId { get; init; } = default!;

    public string? NextSigningKeyId { get; init; }

    public string? SigningKeyPem { get; init; }

    public string? NextSigningKeyPem { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasNextId = !string.IsNullOrWhiteSpace(NextSigningKeyId);
        var hasNextPem = !string.IsNullOrWhiteSpace(NextSigningKeyPem);

        if (hasNextId != hasNextPem)
        {
            yield return new ValidationResult(
                $"'{nameof(NextSigningKeyId)}' and '{nameof(NextSigningKeyPem)}' must be supplied " +
                "together or not at all; a next signing key needs both its id and its PEM.",
                new[] { nameof(NextSigningKeyId), nameof(NextSigningKeyPem) });
        }
    }
}
