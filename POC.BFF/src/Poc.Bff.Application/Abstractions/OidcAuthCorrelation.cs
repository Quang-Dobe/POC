namespace Poc.Bff.Application.Abstractions;

public sealed record OidcAuthCorrelation(
    string State,
    string CodeVerifier,
    string Nonce);
