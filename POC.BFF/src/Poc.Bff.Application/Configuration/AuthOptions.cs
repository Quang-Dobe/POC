namespace Poc.Bff.Application.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string Authority { get; init; } = default!;

    [Required]
    public string Audience { get; init; } = default!;

    public bool RequireHttpsMetadata { get; init; } = true;

    [Required]
    public string ClientId { get; init; } = default!;

    [Required]
    public string RedirectUri { get; init; } = default!;

    [Required]
    public string FrontendReturnUrl { get; init; } = default!;

    public string? OidcClientSecret { get; init; }
}
