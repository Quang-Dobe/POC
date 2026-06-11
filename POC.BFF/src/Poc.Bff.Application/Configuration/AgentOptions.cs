namespace Poc.Bff.Application.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    [Required]
    public string Host { get; init; } = default!;

    [Required]
    public int Port { get; init; }

    [Range(1, int.MaxValue)]
    public int AskTimeoutSeconds { get; init; } = 100;
}
