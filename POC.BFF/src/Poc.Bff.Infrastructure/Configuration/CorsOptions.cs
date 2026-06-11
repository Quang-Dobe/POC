namespace Poc.Bff.Infrastructure.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    [Required]
    public string SpaOrigin { get; init; } = default!;
}
