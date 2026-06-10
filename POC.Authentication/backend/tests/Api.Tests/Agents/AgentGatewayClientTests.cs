namespace Api.Tests.Agents;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Agents;
using Xunit;

public class AgentGatewayClientTests
{
    private static (AgentGatewayClient client, List<HttpRequestMessage> captured) MakeClient(
        HttpStatusCode status, object responseBody, string agentToken = "agent-token")
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new FakeHandler(status, responseBody, captured);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://agent-gw") };
        var tokenProvider = new FakeTokenProvider(agentToken);
        return (new AgentGatewayClient(httpClient, tokenProvider), captured);
    }

    [Fact]
    public async Task AskAsync_sends_agent_token_not_user_token_in_authorization_header()
    {
        var (client, captured) = MakeClient(HttpStatusCode.OK,
            new { answer = "test answer", session_id = "s1" }, agentToken: "agent-acquired-token");

        await client.AskAsync("what is 42?", null, "user-bearer-token");

        Assert.Single(captured);
        Assert.Equal("Bearer", captured[0].Headers.Authorization?.Scheme);
        Assert.Equal("agent-acquired-token", captured[0].Headers.Authorization?.Parameter);
        Assert.NotEqual("user-bearer-token", captured[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task AskAsync_posts_to_slash_ask()
    {
        var (client, captured) = MakeClient(HttpStatusCode.OK,
            new { answer = "ok", session_id = "s2" });

        await client.AskAsync("q", null, "tok");

        Assert.Equal("/ask", captured[0].RequestUri?.AbsolutePath);
        Assert.Equal(HttpMethod.Post, captured[0].Method);
    }

    [Fact]
    public async Task AskAsync_returns_deserialized_response()
    {
        var (client, _) = MakeClient(HttpStatusCode.OK,
            new { answer = "the answer", session_id = "thread-abc" });

        var result = await client.AskAsync("q", null, "tok");

        Assert.Equal("the answer", result.Answer);
        Assert.Equal("thread-abc", result.SessionId);
    }

    [Fact]
    public async Task AskAsync_includes_session_id_in_request_body_when_provided()
    {
        var (client, captured) = MakeClient(HttpStatusCode.OK,
            new { answer = "ok", session_id = "s3" });

        await client.AskAsync("q", "existing-session", "tok");

        var body = await captured[0].Content!.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("existing-session", body.GetProperty("session_id").GetString());
    }

    private class FakeTokenProvider(string token) : IAgentTokenProvider
    {
        public Task<string> GetTokenAsync(string userToken, CancellationToken ct = default)
            => Task.FromResult(token);
    }

    private class FakeHandler(HttpStatusCode status, object body, List<HttpRequestMessage> captured)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            captured.Add(request);
            return new HttpResponseMessage(status)
            {
                Content = JsonContent.Create(body)
            };
        }
    }
}
