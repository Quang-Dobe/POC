namespace Api.Agents;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.Configuration;
using Microsoft.Extensions.Options;

public sealed class KeycloakAgentTokenProvider : IAgentTokenProvider, IDisposable
{
    private readonly AgentOptions _options;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _expiry = DateTimeOffset.MinValue;

    public KeycloakAgentTokenProvider(IOptions<AgentOptions> options, HttpClient http)
    {
        _options = options.Value;
        _http = http;
    }

    public async Task<string> GetTokenAsync(string userToken, CancellationToken ct = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiry.AddSeconds(-30))
            return _cachedToken;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiry.AddSeconds(-30))
                return _cachedToken;

            var endpoint = _options.TokenEndpoint
                ?? throw new InvalidOperationException("Agent:TokenEndpoint is required in DEV.");

            var resp = await _http.PostAsync(endpoint, new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _options.ClientId),
                new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            ]), ct);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<TokenResponse>(ct)
                ?? throw new InvalidOperationException("Empty token response from Keycloak.");

            _cachedToken = body.AccessToken;
            _expiry = DateTimeOffset.UtcNow.AddSeconds(body.ExpiresIn);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
