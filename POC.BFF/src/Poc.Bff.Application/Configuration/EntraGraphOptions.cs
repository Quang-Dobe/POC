namespace Poc.Bff.Application.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Microsoft Graph client-credentials settings the Entra invite provisioner (PROD) uses to
/// send a B2B guest invitation (<c>POST /invitations</c>) so the invited user receives an
/// email with a single-use redeem URL. The client secret is read from the secret-store seam
/// (Key Vault), never from this file.
/// </summary>
public sealed class EntraGraphOptions
{
    public const string SectionName = "EntraGraph";

    public string BaseUrl { get; init; } = "https://graph.microsoft.com/v1.0";

    public string Authority { get; init; } = "https://login.microsoftonline.com";

    public string Scope { get; init; } = "https://graph.microsoft.com/.default";

    [Required]
    public string TenantId { get; init; } = default!;

    [Required]
    public string ClientId { get; init; } = default!;

    public string? ClientSecret { get; init; }
}
