using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Domain.Invites;
using Poc.Bff.Infrastructure.Rbac;
using Poc.Bff.Integration.Tests.Support;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Poc.Bff.Integration.Tests;

public class InviteEndpointTests : IClassFixture<InviteEndpointTests.InviteFactory>
{
    private const string SessionCookieName = "poc.session";

    private readonly InviteFactory _factory;

    public InviteEndpointTests(InviteFactory factory) => _factory = factory;

    private static HttpClient ClientNoRedirect(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    [Fact]
    public async Task Invite_WithoutSession_Returns401()
    {
        using var client = ClientNoRedirect(_factory);

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/invite")
        {
            Content = JsonContent.Create(new { username = "erin@example.com" }),
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invite_AsReader_Returns403()
    {
        using var client = ClientNoRedirect(_factory);
        var cookie = SessionCookieTicketIssuer.IssueCookiePair(
            _factory.Services, SessionCookieName, "reader-user", "Rita Reader", "Oslo",
            RbacPolicies.ReaderRole);

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/invite")
        {
            Content = JsonContent.Create(new { username = "erin@example.com" }),
        };
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invite_AsManager_Returns200_WithInviteResponse()
    {
        using var client = ClientNoRedirect(_factory);
        var cookie = SessionCookieTicketIssuer.IssueCookiePair(
            _factory.Services, SessionCookieName, "manager-user", "Mona Manager", "Oslo",
            RbacPolicies.ManagerRole);

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/invite")
        {
            Content = JsonContent.Create(new { username = "erin@example.com" }),
        };
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InviteBody>();
        Assert.NotNull(body);
        Assert.Equal("erin@example.com", body!.Subject);
        Assert.False(body.AlreadyExisted);
        Assert.Equal("Stub-Generated-Pw-1!", body.GeneratedPassword);
    }

    [Fact]
    public async Task Invite_WhenProvisionerThrows_Returns502()
    {
        using var factory = new ThrowingInviteFactory();
        using var client = ClientNoRedirect(factory);
        var cookie = SessionCookieTicketIssuer.IssueCookiePair(
            factory.Services, SessionCookieName, "manager-user", "Mona Manager", "Oslo",
            RbacPolicies.ManagerRole);

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/invite")
        {
            Content = JsonContent.Create(new { username = "erin@example.com" }),
        };
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private sealed record InviteBody(string Subject, string RedeemUrl, bool AlreadyExisted, string GeneratedPassword);

    public sealed class InviteFactory : BffWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Invite:DefaultRoles:0"] = "reader",
                    ["Invite:DefaultRegion"] = "Manhattan County",
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInviteProvisioner>();
                services.AddSingleton<IInviteProvisioner>(new StubInviteProvisioner());
            });
        }
    }

    private sealed class ThrowingInviteFactory : BffWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Invite:DefaultRoles:0"] = "reader",
                    ["Invite:DefaultRegion"] = "Manhattan County",
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInviteProvisioner>();
                var throwingProvisioner = Substitute.For<IInviteProvisioner>();
                throwingProvisioner
                    .ProvisionAsync(Arg.Any<InviteRequest>(), Arg.Any<CancellationToken>())
                    .ThrowsAsync(new InviteException("IdP unavailable."));
                services.AddSingleton(throwingProvisioner);
            });
        }
    }

    private sealed class StubInviteProvisioner : IInviteProvisioner
    {
        public Task<InviteOutcome> ProvisionAsync(InviteRequest request, CancellationToken ct = default) =>
            Task.FromResult(new InviteOutcome(
                Subject: request.Username,
                RedeemUrl: "https://invite.stub/redeem/x",
                AlreadyExisted: false,
                GeneratedPassword: "Stub-Generated-Pw-1!"));
    }
}
