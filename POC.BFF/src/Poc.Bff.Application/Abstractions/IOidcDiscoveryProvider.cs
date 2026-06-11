namespace Poc.Bff.Application.Abstractions;

public interface IOidcDiscoveryProvider
{
    Task<OidcDiscoveryDocument> GetAsync(CancellationToken ct = default);
}
