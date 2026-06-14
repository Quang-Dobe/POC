namespace Poc.Bff.Application.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Microsoft Graph client-credentials settings the Entra invite provisioner (PROD) uses to
/// create a member user with an auto-generated default password. The client secret is read
/// from the secret-store seam (Key Vault), never from this file.
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

    /// <summary>
    /// The verified domain new member users are created under (e.g. "contoso.onmicrosoft.com").
    /// The userPrincipalName becomes "{mailNickname}@{UserDomain}".
    /// </summary>
    [Required]
    public string UserDomain { get; init; } = default!;
}
