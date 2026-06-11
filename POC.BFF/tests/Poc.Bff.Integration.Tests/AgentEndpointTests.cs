using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Infrastructure.Rbac;
using Poc.Bff.Integration.Tests.Support;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Xunit;

namespace Poc.Bff.Integration.Tests;

public class AgentEndpointTests : IClassFixture<AgentEndpointTests.Factory>
{
    private const string SessionCookieName = "poc.session";
    private const string StubAnswer = "stub-answer-from-integration";
    private static readonly string[] StreamChunks = { "frame1", "frame2", "frame3" };

    private readonly Factory _factory;

    public AgentEndpointTests(Factory factory) => _factory = factory;

    private static HttpClient ClientNoCookieContainer(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    [Fact]
    public async Task PostApiAsk_WithoutCookie_Returns401()
    {
        using var client = ClientNoCookieContainer(_factory);

        var response = await client.PostAsJsonAsync("/api/ask",
            new { question = "hello", session_id = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostApiAsk_WithDisallowedRole_Returns403()
    {
        using var client = ClientNoCookieContainer(_factory);
        SeedSessionCookie(client, RbacPolicies.OutOfSetRole);

        var response = await client.PostAsJsonAsync("/api/ask",
            new { question = "hello", session_id = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RbacPolicies.ReaderRole)]
    [InlineData(RbacPolicies.ManagerRole)]
    public async Task PostApiAsk_WithAllowedRole_Returns200WithAgentResponse(string role)
    {
        using var client = ClientNoCookieContainer(_factory);
        SeedSessionCookie(client, role);

        var response = await client.PostAsJsonAsync("/api/ask",
            new { question = "hello", session_id = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AgentResponse>();
        Assert.NotNull(payload);
        Assert.Equal(StubAnswer, payload!.Answer);
    }

    [Fact]
    public async Task PostApiAskStream_WithoutCookie_Returns401()
    {
        using var client = ClientNoCookieContainer(_factory);

        var response = await client.PostAsJsonAsync("/api/ask/stream",
            new { question = "hello", session_id = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostApiAskStream_WithDisallowedRole_Returns403()
    {
        using var client = ClientNoCookieContainer(_factory);
        SeedSessionCookie(client, RbacPolicies.OutOfSetRole);

        var response = await client.PostAsJsonAsync("/api/ask/stream",
            new { question = "hello", session_id = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RbacPolicies.ReaderRole)]
    [InlineData(RbacPolicies.ManagerRole)]
    public async Task PostApiAskStream_WithAllowedRole_Returns200WithSseFrames(string role)
    {
        using var client = ClientNoCookieContainer(_factory);
        SeedSessionCookie(client, role);

        var response = await client.PostAsJsonAsync("/api/ask/stream",
            new { question = "hello", session_id = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        foreach (var chunk in StreamChunks)
        {
            Assert.Contains($"data: {chunk}\n\n", body);
        }
    }

    private void SeedSessionCookie(HttpClient client, string role)
    {
        var cookiePair = SessionCookieTicketIssuer.IssueCookiePair(
            _factory.Services,
            SessionCookieName,
            subject: "user-123",
            displayName: "Test User",
            region: "Oslo",
            role);

        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookiePair);
    }

    public sealed class Factory : BffWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAgentGatewayClient>();
                services.AddSingleton<IAgentGatewayClient>(new StubAgentClient());
            });
        }
    }

    private sealed class StubAgentClient : IAgentGatewayClient
    {
        public Task<AgentResponse> AskAsync(
            string question,
            string? sessionId,
            string token,
            CancellationToken ct = default)
        {
            return Task.FromResult(new AgentResponse(StubAnswer, sessionId ?? "stub-session"));
        }

        public async IAsyncEnumerable<string> AskStreamAsync(
            string question,
            string? sessionId,
            string token,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var chunk in StreamChunks)
            {
                ct.ThrowIfCancellationRequested();
                yield return chunk;
                await Task.Yield();
            }
        }
    }
}
