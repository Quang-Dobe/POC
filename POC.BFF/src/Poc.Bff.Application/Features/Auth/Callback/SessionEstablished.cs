namespace Poc.Bff.Application.Features.Auth.Callback;

public sealed record SessionEstablished(
    string Subject,
    string DisplayName,
    string Region,
    IReadOnlyList<string> Roles,
    string RedirectUrl);
