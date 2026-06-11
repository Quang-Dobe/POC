namespace Poc.Bff.Application.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class KeycloakAdminOptions
{
    public const string SectionName = "Keycloak";

    [Required]
    public string AdminBaseUrl { get; init; } = default!;

    [Required]
    public string Realm { get; init; } = default!;

    [Required]
    public string AdminClientId { get; init; } = default!;

    public string? AdminClientSecret { get; init; }
}
