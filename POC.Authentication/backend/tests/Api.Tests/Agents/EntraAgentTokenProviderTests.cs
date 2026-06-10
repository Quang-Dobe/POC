namespace Api.Tests.Agents;

using System.Net;
using System.Net.Http.Json;
using Api.Agents;
using Api.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

public class EntraAgentTokenProviderTests
{
    private static (EntraAgentTokenProvider provider, List<HttpRequestMessage> captured)
        MakeProvider(HttpStatusCode status, object responseBody)
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new FakeHandler(status, responseBody, captured);
        var opts = Options.Create(new AgentOptions
        {
            Host = "localhost",
            Port = 8082,
            ClientId = "api-client-id",
            ClientSecret = "api-secret",
            TokenEndpoint = "https://login.microsoftonline.com/tenant/oauth2/v2.0/token",
            AgentScope = "api://agent-client-id/.default"
        });
        return (new EntraAgentTokenProvider(opts, new HttpClient(handler)), captured);
    }

    [Fact]
    public async Task GetTokenAsync_returns_access_token_from_entra_response()
    {
        var (provider, _) = MakeProvider(HttpStatusCode.OK,
            new { access_token = "entra-obo-token" });

        var token = await provider.GetTokenAsync("user-token-xyz");

        Assert.Equal("entra-obo-token", token);
    }

    [Fact]
    public async Task GetTokenAsync_posts_obo_grant_with_user_token_as_assertion()
    {
        var (provider, captured) = MakeProvider(HttpStatusCode.OK,
            new { access_token = "t" });

        await provider.GetTokenAsync("user-bearer-abc");

        Assert.Single(captured);
        var form = await captured[0].Content!.ReadAsStringAsync();
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", form);
        Assert.Contains("assertion=user-bearer-abc", form);
        Assert.Contains("requested_token_use=on_behalf_of", form);
        Assert.Contains("scope=api%3A%2F%2Fagent-client-id%2F.default", form);
    }

    [Fact]
    public async Task GetTokenAsync_throws_when_agent_scope_is_null()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, new { access_token = "t" }, []);
        var provider = new EntraAgentTokenProvider(
            Options.Create(new AgentOptions
            {
                Host = "h", Port = 1,
                ClientId = "c", ClientSecret = "s",
                TokenEndpoint = "https://entra/token",
                AgentScope = null
            }), new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetTokenAsync("user-token"));
    }

    [Fact]
    public async Task GetTokenAsync_throws_when_token_endpoint_is_null()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, new { access_token = "t" }, []);
        var provider = new EntraAgentTokenProvider(
            Options.Create(new AgentOptions
            {
                Host = "h", Port = 1,
                ClientId = "c", ClientSecret = "s",
                TokenEndpoint = null,
                AgentScope = "api://x/.default"
            }), new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetTokenAsync("user-token"));
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
