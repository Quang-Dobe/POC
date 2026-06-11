using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Integration.Tests.Support;
using Xunit;

namespace Poc.Bff.Integration.Tests;

public class AuthEndpointTests : IClassFixture<AuthEndpointTests.AuthFactory>
{
    private const string SessionCookieName = "poc.session";
    private const string CorrelationCookieName = "poc.oidc";

    private readonly AuthFactory _factory;

    public AuthEndpointTests(AuthFactory factory) => _factory = factory;

    private static HttpClient ClientNoRedirect(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    [Fact]
    public async Task Login_Returns302_RedirectToIdpAuthorizeEndpoint()
    {
        using var client = ClientNoRedirect(_factory);

        var response = await client.GetAsync("/auth/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("https://idp.example/authorize", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_SetsCorrelationCookie_AndNotSessionCookie()
    {
        using var client = ClientNoRedirect(_factory);

        var response = await client.GetAsync("/auth/login");

        var setCookieHeader = string.Join(";", response.Headers.GetValues("Set-Cookie"));
        Assert.Contains($"{CorrelationCookieName}=", setCookieHeader, StringComparison.Ordinal);
        Assert.DoesNotContain($"{SessionCookieName}=", setCookieHeader, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_IsAnonymous_NoSessionCookieRequired()
    {
        using var client = ClientNoRedirect(_factory);

        var response = await client.GetAsync("/auth/login");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutCookie_Returns401()
    {
        using var client = ClientNoRedirect(_factory);

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidSessionCookie_Returns200_WithExpectedUserJson()
    {
        using var client = ClientNoRedirect(_factory);

        var cookie = SessionCookieTicketIssuer.IssueCookiePair(
            _factory.Services,
            SessionCookieName,
            subject: "user-123",
            displayName: "Ada Lovelace",
            region: "Oslo",
            "manager");

        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeBody>();
        Assert.NotNull(body);
        Assert.Equal("Ada Lovelace", body!.DisplayName);
        Assert.Equal(new[] { "manager" }, body.Roles);
    }

    [Fact]
    public async Task Logout_WithoutCookie_Returns401()
    {
        using var client = ClientNoRedirect(_factory);

        var response = await client.PostAsync("/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithValidSessionCookie_Returns204_AndClearsCookie()
    {
        using var client = ClientNoRedirect(_factory);

        var cookie = SessionCookieTicketIssuer.IssueCookiePair(
            _factory.Services,
            SessionCookieName,
            subject: "user-123",
            displayName: "Ada Lovelace",
            region: "Oslo",
            "manager");

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join(";", values)
            : string.Empty;
        Assert.Contains($"{SessionCookieName}=", setCookies, StringComparison.Ordinal);
    }

    private sealed record MeBody(string DisplayName, IReadOnlyList<string> Roles);

    public sealed class AuthFactory : BffWebApplicationFactory
    {
        private readonly IOidcAuthClient _stubOidcAuthClient = Substitute.For<IOidcAuthClient>();

        public AuthFactory()
        {
            _stubOidcAuthClient
                .BuildAuthorizeUrlAsync(Arg.Any<OidcAuthCorrelation>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var correlation = callInfo.Arg<OidcAuthCorrelation>();
                    var url = QueryHelpers.AddQueryString(
                        "https://idp.example/authorize",
                        new Dictionary<string, string?> { ["state"] = correlation.State });
                    return Task.FromResult(url);
                });
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Session:CookieName"] = SessionCookieName,
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOidcAuthClient>();
                services.AddSingleton(_stubOidcAuthClient);
            });
        }
    }
}
