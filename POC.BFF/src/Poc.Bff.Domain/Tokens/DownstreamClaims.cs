namespace Poc.Bff.Domain.Tokens;

public sealed record DownstreamClaims(
    string? Sub,
    IReadOnlyList<string>? Roles,
    string? Region);
