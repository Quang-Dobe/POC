namespace Api.Configuration;

using System.ComponentModel.DataAnnotations;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    [Required]
    public string Host { get; init; } = default!;

    [Required]
    public int Port { get; init; }

    [Required]
    public string ClientId { get; init; } = default!;

    [Required]
    public string ClientSecret { get; init; } = default!;

    public string? TokenEndpoint { get; init; }

    public string? AgentScope { get; init; }
}
