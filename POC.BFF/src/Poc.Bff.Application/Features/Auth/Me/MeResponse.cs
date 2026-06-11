namespace Poc.Bff.Application.Features.Auth.Me;

public sealed record MeResponse(string DisplayName, IReadOnlyList<string> Roles);
