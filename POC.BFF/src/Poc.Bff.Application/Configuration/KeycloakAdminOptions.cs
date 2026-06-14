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

    /// <summary>
    /// The OIDC client the invite action-email link is scoped to. Keycloak validates
    /// <see cref="RedeemRedirectUri"/> against this client's registered redirect URIs.
    /// </summary>
    public string? RedeemClientId { get; init; }

    /// <summary>
    /// Where Keycloak returns the invited user after they complete the required actions
    /// (set password / verify email) from the emailed single-use link.
    /// </summary>
    public string? RedeemRedirectUri { get; init; }
}
