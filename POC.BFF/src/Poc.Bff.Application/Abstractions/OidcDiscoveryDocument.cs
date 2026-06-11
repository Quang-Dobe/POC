namespace Poc.Bff.Application.Abstractions;

public sealed record OidcDiscoveryDocument(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string JwksUri);
