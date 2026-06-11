namespace Poc.Bff.Application.Features.Auth.Login;

public sealed record LoginRedirect(
    string AuthorizeUrl,
    string State,
    string CodeVerifier,
    string Nonce);
