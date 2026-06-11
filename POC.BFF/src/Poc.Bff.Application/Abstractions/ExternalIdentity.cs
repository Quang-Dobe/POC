namespace Poc.Bff.Application.Abstractions;

public sealed record ExternalIdentity(
    string Subject,
    string DisplayName);
