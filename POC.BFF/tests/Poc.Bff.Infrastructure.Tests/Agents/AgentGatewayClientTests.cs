namespace Poc.Bff.Infrastructure.Tests.Agents;

using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Infrastructure.Agents;
using Xunit;

public class AgentGatewayClientTests
{
    private static (AgentGatewayClient client, List<HttpRequestMessage> captured) MakeClient(
        HttpStatusCode status, object responseBody)
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new FakeHandler(status, responseBody, captured);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://agent-gw") };
        var options = Options.Create(new AgentOptions { Host = "agent-gw", Port = 80 });
        return (new AgentGatewayClient(httpClient, options), captured);
    }

    [Fact]
    public async Task AskAsync_sends_the_internal_token_verbatim_on_authorization_bearer()
    {
        var (client, captured) = MakeClient(HttpStatusCode.OK,
            new { answer = "test answer", session_id = "s1" });

        await client.AskAsync("what is 42?", null, "internal-token");

        Assert.Single(captured);
        Assert.Equal("Bearer", captured[0].Headers.Authorization?.Scheme);
        Assert.Equal("internal-token", captured[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task AskAsync_sends_no_second_token_header()
    {
        var (client, captured) = MakeClient(HttpStatusCode.OK,
            new { answer = "ok", session_id = "s2" });

        await client.AskAsync("q", null, "internal-token");

        Assert.False(captured[0].Headers.TryGetValues("X-Dab-Token", out _));
    }

    [Fact]
    public async Task AskAsync_posts_to_slash_ask()
    {
        var (client, captured) = MakeClient(HttpStatusCode.OK,
            new { answer = "ok", session_id = "s2" });

        await client.AskAsync("q", null, "internal-token");

        Assert.Equal("/ask", captured[0].RequestUri?.AbsolutePath);
        Assert.Equal(HttpMethod.Post, captured[0].Method);
    }

    [Fact]
    public async Task AskAsync_returns_deserialized_response()
    {
        var (client, _) = MakeClient(HttpStatusCode.OK,
            new { answer = "the answer", session_id = "thread-abc" });

        var result = await client.AskAsync("q", null, "internal-token");

        Assert.Equal("the answer", result.Answer);
        Assert.Equal("thread-abc", result.SessionId);
    }

    [Fact]
    public async Task AskAsync_includes_session_id_in_request_body_when_provided()
    {
        var (client, captured) = MakeClient(HttpStatusCode.OK,
            new { answer = "ok", session_id = "s3" });

        await client.AskAsync("q", "existing-session", "internal-token");

        var body = await captured[0].Content!.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("existing-session", body.GetProperty("session_id").GetString());
    }

    private sealed class FakeHandler(HttpStatusCode status, object body, List<HttpRequestMessage> captured)
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
