namespace Poc.Bff.Infrastructure.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class SessionOptions
{
    public const string SectionName = "Session";

    [Required]
    public string CookieName { get; init; } = default!;

    [Range(1, int.MaxValue)]
    public int TtlMinutes { get; init; }

    [Required]
    public string RedisAddress { get; init; } = default!;
}
