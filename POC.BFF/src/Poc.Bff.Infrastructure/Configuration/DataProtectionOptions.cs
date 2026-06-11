namespace Poc.Bff.Infrastructure.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class DataProtectionOptions
{
    public const string SectionName = "DataProtection";

    [Required]
    public string ApplicationName { get; init; } = default!;

    [Required]
    public string RedisKey { get; init; } = default!;
}
