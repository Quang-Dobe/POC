namespace Poc.Bff.Application.Abstractions;

public interface IOidcAuthClient
{
    Task<string> BuildAuthorizeUrlAsync(OidcAuthCorrelation correlation, CancellationToken ct = default);

    Task<ExternalIdentity> ExchangeCodeAsync(
        string code,
        OidcAuthCorrelation correlation,
        CancellationToken ct = default);
}
