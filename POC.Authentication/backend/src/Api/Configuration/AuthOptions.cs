namespace Api.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string Authority { get; init; } = default!;

    [Required]
    public string Audience { get; init; } = default!;

    public bool RequireHttpsMetadata { get; init; } = true;
}
