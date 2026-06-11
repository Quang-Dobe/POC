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
using Poc.Bff.Domain.Rbac;
using Poc.Bff.Integration.Tests.Support;
using Xunit;

namespace Poc.Bff.Integration.Tests.Features.Auth;

public class CallbackFullFlowTests : IClassFixture<CallbackFullFlowTests.CallbackFactory>
{
    private const string SessionCookieName = "poc.session";
    private const string CorrelationCookieName = "poc.oidc";
    private const string FrontendReturnUrl = "https://bff.test.local";

    private const string Subject = "manager-user";
    private const string DisplayName = "Ada Lovelace";
    private const string Region = "Oslo";

    private readonly CallbackFactory _factory;

    public CallbackFullFlowTests(CallbackFactory factory) => _factory = factory;

    private static HttpClient ClientNoRedirect(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    [Fact]
    public async Task FullFlow_LoginCallbackMe_EstablishesSession_AndMeReturnsExpectedUser()
    {
        using var client = ClientNoRedirect(_factory);

        var loginResponse = await client.GetAsync("/auth/login");

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.NotNull(loginResponse.Headers.Location);

        var correlationCookie = ReadSetCookiePair(loginResponse, CorrelationCookieName);
        Assert.NotEmpty(correlationCookie);

        var state = ReadStateFromAuthorizeUrl(loginResponse.Headers.Location!);
        Assert.NotEmpty(state);

        var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/auth/callback?code=stub-code&state={Uri.EscapeDataString(state)}");
        callbackRequest.Headers.Add("Cookie", correlationCookie);

        var callbackResponse = await client.SendAsync(callbackRequest);

        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        Assert.NotNull(callbackResponse.Headers.Location);
        Assert.StartsWith(FrontendReturnUrl, callbackResponse.Headers.Location!.ToString(), StringComparison.Ordinal);

        Assert.DoesNotContain("error=", callbackResponse.Headers.Location!.ToString(), StringComparison.Ordinal);
        var sessionCookie = ReadSetCookiePair(callbackResponse, SessionCookieName);
        Assert.NotEmpty(sessionCookie);

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        meRequest.Headers.Add("Cookie", sessionCookie);
        var meResponse = await client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var body = await meResponse.Content.ReadFromJsonAsync<MeBody>();
        Assert.NotNull(body);
        Assert.Equal(DisplayName, body!.DisplayName);
        Assert.Contains("manager", body.Roles);
    }

    [Fact]
    public async Task Callback_MissingCorrelationCookie_RedirectsToFrontendError()
    {
        using var client = ClientNoRedirect(_factory);

        var response = await client.GetAsync("/auth/callback?code=any-code&state=any-state");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("error=access_denied", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Callback_StateMismatch_RedirectsToFrontendError()
    {
        using var client = ClientNoRedirect(_factory);

        var loginResponse = await client.GetAsync("/auth/login");
        var correlationCookie = ReadSetCookiePair(loginResponse, CorrelationCookieName);

        var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/auth/callback?code=stub-code&state=tampered-state");
        callbackRequest.Headers.Add("Cookie", correlationCookie);

        var response = await client.SendAsync(callbackRequest);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("error=access_denied", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Callback_MissingCode_RedirectsToFrontendError()
    {
        using var client = ClientNoRedirect(_factory);

        var loginResponse = await client.GetAsync("/auth/login");
        var correlationCookie = ReadSetCookiePair(loginResponse, CorrelationCookieName);
        var state = ReadStateFromAuthorizeUrl(loginResponse.Headers.Location!);

        var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/auth/callback?state={Uri.EscapeDataString(state)}");
        callbackRequest.Headers.Add("Cookie", correlationCookie);

        var response = await client.SendAsync(callbackRequest);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=access_denied", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    private static string ReadStateFromAuthorizeUrl(Uri authorizeUrl) =>
        QueryHelpers.ParseQuery(authorizeUrl.Query)["state"].ToString();

    private static string ReadSetCookiePair(HttpResponseMessage response, string cookieName) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.First(c => c.StartsWith($"{cookieName}=", StringComparison.Ordinal)).Split(';')[0]
            : string.Empty;

    private sealed record MeBody(string DisplayName, IReadOnlyList<string> Roles);

    public sealed class CallbackFactory : BffWebApplicationFactory
    {
        private readonly IOidcAuthClient _stubOidcAuthClient = Substitute.For<IOidcAuthClient>();
        private readonly IRoleResolver _stubRoleResolver = Substitute.For<IRoleResolver>();

        public CallbackFactory()
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

            _stubOidcAuthClient
                .ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<OidcAuthCorrelation>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new ExternalIdentity(Subject, DisplayName)));

            _stubRoleResolver
                .Resolve(Arg.Any<string>())
                .Returns(RoleResolution.Allowed(new[] { "manager" }, Region));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Session:CookieName"] = SessionCookieName,
                    ["Auth:FrontendReturnUrl"] = FrontendReturnUrl,
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOidcAuthClient>();
                services.AddSingleton(_stubOidcAuthClient);

                services.RemoveAll<IRoleResolver>();
                services.AddSingleton(_stubRoleResolver);
            });
        }
    }
}
