namespace Api.Agents;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.Configuration;
using Microsoft.Extensions.Options;

public sealed class EntraAgentTokenProvider(IOptions<AgentOptions> options, HttpClient http)
    : IAgentTokenProvider
{
    private readonly AgentOptions _options = options.Value;

    public async Task<string> GetTokenAsync(string userToken, CancellationToken ct = default)
    {
        var endpoint = _options.TokenEndpoint
            ?? throw new InvalidOperationException("Agent:TokenEndpoint is required in PROD.");
        var scope = _options.AgentScope
            ?? throw new InvalidOperationException("Agent:AgentScope is required in PROD.");

        var resp = await http.PostAsync(endpoint, new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
            new KeyValuePair<string, string>("assertion", userToken),
            new KeyValuePair<string, string>("scope", scope),
            new KeyValuePair<string, string>("client_id", _options.ClientId),
            new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            new KeyValuePair<string, string>("requested_token_use", "on_behalf_of"),
        ]), ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Entra OBO token exchange failed ({(int)resp.StatusCode}): {err}");
        }

        var body = await resp.Content.ReadFromJsonAsync<TokenResponse>(ct)
            ?? throw new InvalidOperationException("Empty token response from Entra.");

        return body.AccessToken;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);
}
