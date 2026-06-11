namespace Poc.Bff.Infrastructure.Oidc;

using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

public sealed class OidcDiscoveryProvider : IOidcDiscoveryProvider
{
    public const string HttpClientName = "OidcDiscovery";

    private const string DiscoverySuffix = "/.well-known/openid-configuration";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OidcDiscoveryProvider> _logger;
    private readonly string _discoveryUri;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private OidcDiscoveryDocument? _cached;

    public OidcDiscoveryProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthOptions> options,
        ILogger<OidcDiscoveryProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _discoveryUri = options.Value.Authority.TrimEnd('/') + DiscoverySuffix;
    }

    public async Task<OidcDiscoveryDocument> GetAsync(CancellationToken ct = default)
    {
        var cached = _cached;
        if (cached is not null)
        {
            return cached;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var document = await FetchAsync(ct).ConfigureAwait(false);
            _cached = document;
            _logger.LogInformation(
                "OIDC discovery loaded from the external IDP (issuer {Issuer}).",
                document.Issuer);
            return document;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<OidcDiscoveryDocument> FetchAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync(_discoveryUri, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var configuration = OpenIdConnectConfiguration.Create(json);

        if (string.IsNullOrWhiteSpace(configuration.Issuer)
            || string.IsNullOrWhiteSpace(configuration.AuthorizationEndpoint)
            || string.IsNullOrWhiteSpace(configuration.TokenEndpoint)
            || string.IsNullOrWhiteSpace(configuration.JwksUri))
        {
            throw new InvalidOperationException(
                "The external IDP OIDC discovery document is missing one of " +
                "issuer/authorization_endpoint/token_endpoint/jwks_uri.");
        }

        return new OidcDiscoveryDocument(
            Issuer: configuration.Issuer,
            AuthorizationEndpoint: configuration.AuthorizationEndpoint,
            TokenEndpoint: configuration.TokenEndpoint,
            JwksUri: configuration.JwksUri);
    }
}
