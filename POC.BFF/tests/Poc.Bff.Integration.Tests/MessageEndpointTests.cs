using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Poc.Bff.Integration.Tests.Support;
using Xunit;

namespace Poc.Bff.Integration.Tests;

public class MessageEndpointTests : IClassFixture<MessageEndpointTests.Factory>
{
    private const string SeededDisplayString = "integration-test-message-abc123";
    private const string SessionCookieName = "poc.session";

    private readonly Factory _factory;

    public MessageEndpointTests(Factory factory) => _factory = factory;

    private static HttpClient ClientNoCookieContainer(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    [Fact]
    public async Task GetMessage_WithoutCookie_Returns401()
    {
        using var client = ClientNoCookieContainer(_factory);

        var response = await client.GetAsync("/api/message");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMessage_WithValidSessionCookie_Returns200WithSeededDisplayString()
    {
        using var client = ClientNoCookieContainer(_factory);
        SeedSessionCookie(client);

        var response = await client.GetAsync("/api/message");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(payload);
        Assert.Equal(SeededDisplayString, payload!.Message);
    }

    private void SeedSessionCookie(HttpClient client)
    {
        var cookiePair = CookieTicketIssuer.IssueCookiePair(
            _factory.Services,
            SessionCookieName,
            subject: "user-123",
            displayName: "Ada Lovelace",
            region: "Oslo",
            "reader");

        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookiePair);
    }

    private sealed record MessageResponse(string Message);

    public sealed class Factory : BffWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {

            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Message:DisplayString"] = SeededDisplayString,
                    ["Session:CookieName"] = SessionCookieName,
                }));
        }
    }
}
