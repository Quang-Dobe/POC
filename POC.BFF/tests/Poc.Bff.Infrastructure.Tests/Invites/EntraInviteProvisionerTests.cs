namespace Poc.Bff.Infrastructure.Tests.Invites;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Domain.Invites;
using Poc.Bff.Infrastructure.Invites;
using Xunit;

public class EntraInviteProvisionerTests
{
    private static (EntraInviteProvisioner provisioner, RoutingHandler handler) Build(
        HttpStatusCode invitationStatus = HttpStatusCode.Created,
        string? clientSecret = "graph-secret")
    {
        var handler = new RoutingHandler(invitationStatus);
        var factory = new StubHttpClientFactory(handler);

        var graphOptions = Options.Create(new EntraGraphOptions
        {
            BaseUrl = "https://graph.microsoft.com/v1.0",
            Authority = "https://login.microsoftonline.com",
            Scope = "https://graph.microsoft.com/.default",
            TenantId = "tenant-123",
            ClientId = "graph-app",
            ClientSecret = clientSecret,
        });

        var authOptions = Options.Create(new AuthOptions
        {
            Authority = "https://login.microsoftonline.com/tenant/v2.0",
            Audience = "api://x",
            ClientId = "bff",
            RedirectUri = "https://bff/auth/callback",
            FrontendReturnUrl = "https://spa.example",
        });

        var provisioner = new EntraInviteProvisioner(
            factory,
            graphOptions,
            authOptions,
            NullLogger<EntraInviteProvisioner>.Instance);

        return (provisioner, handler);
    }

    [Fact]
    public async Task ProvisionAsync_SendsGuestInvitationEmail_WithRedirectAndMessageFlag()
    {
        var (provisioner, handler) = Build();

        var outcome = await provisioner.ProvisionAsync(new InviteRequest("guest@x.com"));

        Assert.Equal("guest@x.com", outcome.Subject);
        Assert.False(outcome.AlreadyExisted);
        Assert.True(outcome.InvitationSent);

        Assert.NotNull(handler.InvitationBody);
        var body = handler.InvitationBody!.Value;
        Assert.Equal("guest@x.com", body.GetProperty("invitedUserEmailAddress").GetString());
        Assert.Equal("https://spa.example", body.GetProperty("inviteRedirectUrl").GetString());
        Assert.True(body.GetProperty("sendInvitationMessage").GetBoolean());
    }

    [Fact]
    public async Task ProvisionAsync_WhenInvitationFails_ThrowsInviteException()
    {
        var (provisioner, _) = Build(invitationStatus: HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<InviteException>(
            () => provisioner.ProvisionAsync(new InviteRequest("guest@x.com")));
    }

    [Fact]
    public async Task ProvisionAsync_WhenClientSecretMissing_ThrowsInviteException()
    {
        var (provisioner, _) = Build(clientSecret: null);

        await Assert.ThrowsAsync<InviteException>(
            () => provisioner.ProvisionAsync(new InviteRequest("guest@x.com")));
    }

    private sealed class RoutingHandler(HttpStatusCode invitationStatus) : HttpMessageHandler
    {
        public JsonElement? InvitationBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { access_token = "graph-token" }),
                };
            }

            if (path.EndsWith("/invitations", StringComparison.Ordinal))
            {
                InvitationBody = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
                return new HttpResponseMessage(invitationStatus);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
