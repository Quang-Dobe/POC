namespace Api.Tests.Agents;

using System.Net;
using System.Net.Http.Json;
using Api.Agents;
using Api.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

public class KeycloakAgentTokenProviderTests
{
    private static (KeycloakAgentTokenProvider provider, List<HttpRequestMessage> captured)
        MakeProvider(HttpStatusCode status, object responseBody)
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new FakeHandler(status, responseBody, captured);
        var opts = Options.Create(new AgentOptions
        {
            Host = "localhost",
            Port = 8082,
            ClientId = "poc-api",
            ClientSecret = "poc-api-dev-secret",
            TokenEndpoint = "http://fake-kc/token"
        });
        return (new KeycloakAgentTokenProvider(opts, new HttpClient(handler)), captured);
    }

    [Fact]
    public async Task GetTokenAsync_returns_access_token_from_keycloak_response()
    {
        var (provider, _) = MakeProvider(HttpStatusCode.OK,
            new { access_token = "kc-token-abc", expires_in = 300 });

        var token = await provider.GetTokenAsync("any-user-token");

        Assert.Equal("kc-token-abc", token);
    }

    [Fact]
    public async Task GetTokenAsync_posts_client_credentials_grant_to_token_endpoint()
    {
        var (provider, captured) = MakeProvider(HttpStatusCode.OK,
            new { access_token = "t", expires_in = 300 });

        await provider.GetTokenAsync("ignored");

        Assert.Single(captured);
        Assert.Equal(HttpMethod.Post, captured[0].Method);
        var form = await captured[0].Content!.ReadAsStringAsync();
        Assert.Contains("grant_type=client_credentials", form);
        Assert.Contains("client_id=poc-api", form);
        Assert.Contains("client_secret=poc-api-dev-secret", form);
    }

    [Fact]
    public async Task GetTokenAsync_caches_valid_token_and_makes_only_one_http_call()
    {
        var (provider, captured) = MakeProvider(HttpStatusCode.OK,
            new { access_token = "cached", expires_in = 300 });

        var first = await provider.GetTokenAsync("u");
        var second = await provider.GetTokenAsync("u");

        Assert.Equal("cached", first);
        Assert.Equal("cached", second);
        Assert.Single(captured);
    }

    [Fact]
    public async Task GetTokenAsync_throws_when_token_endpoint_is_null()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, new { }, []);
        var provider = new KeycloakAgentTokenProvider(
            Options.Create(new AgentOptions
            {
                Host = "h", Port = 1,
                ClientId = "c", ClientSecret = "s",
                TokenEndpoint = null
            }), new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetTokenAsync("u"));
    }

    private sealed class FakeHandler(
        HttpStatusCode status, object body, List<HttpRequestMessage> captured)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            captured.Add(request);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = JsonContent.Create(body)
            });
        }
    }
}
