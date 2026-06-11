namespace Poc.Bff.Infrastructure.Oidc;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common;
using Poc.Bff.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

public sealed class OidcAuthClient : IOidcAuthClient
{
    private const string PreferredUsernameClaim = "preferred_username";
    private const string NameClaim = "name";
    private const string NonceClaim = "nonce";

    private readonly IOidcDiscoveryProvider _discovery;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OidcAuthClient> _logger;
    private readonly string _clientId;
    private readonly string? _clientSecret;
    private readonly string _redirectUri;

    public OidcAuthClient(
        IOidcDiscoveryProvider discovery,
        IHttpClientFactory httpClientFactory,
        IOptions<AuthOptions> options,
        ILogger<OidcAuthClient> logger)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _discovery = discovery;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var value = options.Value;
        _clientId = value.ClientId;
        _clientSecret = value.OidcClientSecret;
        _redirectUri = value.RedirectUri;
    }

    public async Task<string> BuildAuthorizeUrlAsync(
        OidcAuthCorrelation correlation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(correlation);

        var document = await _discovery.GetAsync(ct).ConfigureAwait(false);
        var challenge = Pkce.ComputeChallenge(correlation.CodeVerifier);

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid profile",
            ["state"] = correlation.State,
            ["nonce"] = correlation.Nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        };

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            document.AuthorizationEndpoint,
            query);
    }

    public async Task<ExternalIdentity> ExchangeCodeAsync(
        string code,
        OidcAuthCorrelation correlation,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(correlation);

        var document = await _discovery.GetAsync(ct).ConfigureAwait(false);
        var idToken = await PostCodeExchangeAsync(document, code, correlation.CodeVerifier, ct)
            .ConfigureAwait(false);

        return await ValidateIdTokenAsync(document, idToken, correlation.Nonce, ct).ConfigureAwait(false);
    }

    private async Task<string> PostCodeExchangeAsync(
        OidcDiscoveryDocument document,
        string code,
        string codeVerifier,
        CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _redirectUri,
            ["client_id"] = _clientId,
            ["code_verifier"] = codeVerifier,
        };

        if (!string.IsNullOrWhiteSpace(_clientSecret))
        {
            form["client_secret"] = _clientSecret;
        }

        var client = _httpClientFactory.CreateClient(OidcDiscoveryProvider.HttpClientName);
        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(document.TokenEndpoint, content, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new OidcAuthException(
                $"The external IDP token exchange failed with status {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(ct).ConfigureAwait(false);
        if (payload is null || string.IsNullOrWhiteSpace(payload.IdToken))
        {
            throw new OidcAuthException("The external IDP token response carried no id_token.");
        }

        return payload.IdToken;
    }

    private async Task<ExternalIdentity> ValidateIdTokenAsync(
        OidcDiscoveryDocument document,
        string idToken,
        string expectedNonce,
        CancellationToken ct)
    {
        var signingKeys = await FetchIdpSigningKeysAsync(document.JwksUri, ct).ConfigureAwait(false);

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = document.Issuer,
            ValidateIssuer = true,
            ValidAudience = _clientId,
            ValidateAudience = true,
            IssuerSigningKeys = signingKeys,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, parameters)
            .ConfigureAwait(false);
        if (!result.IsValid)
        {
            throw new OidcAuthException(
                "The external id_token failed validation (aud/signature/issuer/lifetime).",
                result.Exception ?? new SecurityTokenValidationException("id_token invalid."));
        }

        var nonce = ReadClaim(result.Claims, NonceClaim);
        if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
        {
            throw new OidcAuthException("The external id_token nonce did not match the login nonce.");
        }

        var subject = ReadClaim(result.Claims, PreferredUsernameClaim);
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new OidcAuthException("The external id_token carried no preferred_username.");
        }

        var name = ReadClaim(result.Claims, NameClaim);
        var displayName = string.IsNullOrWhiteSpace(name) ? subject : name;

        _logger.LogInformation("Validated an external id_token and projected the login identity.");
        return new ExternalIdentity(subject, displayName);
    }

    private async Task<IReadOnlyList<SecurityKey>> FetchIdpSigningKeysAsync(
        string jwksUri,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(OidcDiscoveryProvider.HttpClientName);
        using var response = await client.GetAsync(jwksUri, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return new JsonWebKeySet(json).GetSigningKeys().ToList();
    }

    private static string? ReadClaim(IDictionary<string, object>? claims, string claimType) =>
        claims is not null && claims.TryGetValue(claimType, out var value) ? value as string : null;

    private sealed record TokenResponse(
        [property: JsonPropertyName("id_token")] string? IdToken);
}
