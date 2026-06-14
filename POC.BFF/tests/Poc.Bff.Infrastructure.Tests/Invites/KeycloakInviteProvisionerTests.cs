namespace Poc.Bff.Infrastructure.Tests.Invites;

using System.Collections.Generic;
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

public class KeycloakInviteProvisionerTests
{
    private const string NewUserId = "abc-123";

    private static (KeycloakInviteProvisioner provisioner, RoutingHandler handler) Build(
        HttpStatusCode createUserStatus = HttpStatusCode.Created,
        string? adminSecret = "admin-secret")
    {
        var handler = new RoutingHandler(createUserStatus);
        var factory = new StubHttpClientFactory(handler);

        var adminOptions = Options.Create(new KeycloakAdminOptions
        {
            AdminBaseUrl = "https://keycloak.example",
            Realm = "poc",
            AdminClientId = "poc-admin-cli",
            AdminClientSecret = adminSecret,
            RedeemClientId = "poc-spa",
            RedeemRedirectUri = "https://localhost:5173",
        });

        var inviteOptions = Options.Create(new InviteOptions
        {
            DefaultRoles = new[] { "reader" },
            DefaultRegion = "Manhattan County",
            TenantId = "poc",
        });

        var provisioner = new KeycloakInviteProvisioner(
            factory,
            adminOptions,
            inviteOptions,
            NullLogger<KeycloakInviteProvisioner>.Instance);

        return (provisioner, handler);
    }

    [Fact]
    public async Task ProvisionAsync_CreatesUserWithoutCredentials_ThenSendsActionsEmail()
    {
        var (provisioner, handler) = Build();

        var outcome = await provisioner.ProvisionAsync(new InviteRequest("erin@example.com"));

        Assert.Equal("erin@example.com", outcome.Subject);
        Assert.False(outcome.AlreadyExisted);
        Assert.True(outcome.InvitationSent);

        // The create-user body carries no credential — the invited user sets their own password.
        Assert.NotNull(handler.CreateUserBody);
        Assert.False(handler.CreateUserBody!.Value.TryGetProperty("credentials", out _));
        Assert.Equal(
            "UPDATE_PASSWORD",
            handler.CreateUserBody.Value.GetProperty("requiredActions")[0].GetString());

        // The single-use action email was triggered for the created user, scoped to the redeem client.
        Assert.NotNull(handler.ActionsEmailRequestUri);
        Assert.Contains($"/users/{NewUserId}/execute-actions-email", handler.ActionsEmailRequestUri!);
        Assert.Contains("client_id=poc-spa", handler.ActionsEmailRequestUri);
        Assert.Contains("redirect_uri=https", handler.ActionsEmailRequestUri);
        Assert.NotNull(handler.ActionsEmailBody);
        var actions = handler.ActionsEmailBody!.Value;
        Assert.Equal("UPDATE_PASSWORD", actions[0].GetString());
        Assert.Equal("VERIFY_EMAIL", actions[1].GetString());
    }

    [Fact]
    public async Task ProvisionAsync_WhenUserAlreadyExists_ReturnsAlreadyExisted_AndSendsNoEmail()
    {
        var (provisioner, handler) = Build(createUserStatus: HttpStatusCode.Conflict);

        var outcome = await provisioner.ProvisionAsync(new InviteRequest("erin@example.com"));

        Assert.True(outcome.AlreadyExisted);
        Assert.False(outcome.InvitationSent);
        Assert.Null(handler.ActionsEmailRequestUri);
    }

    [Fact]
    public async Task ProvisionAsync_WhenAdminSecretMissing_ThrowsInviteException()
    {
        var (provisioner, _) = Build(adminSecret: null);

        await Assert.ThrowsAsync<InviteException>(
            () => provisioner.ProvisionAsync(new InviteRequest("erin@example.com")));
    }

    private sealed class RoutingHandler(HttpStatusCode createUserStatus) : HttpMessageHandler
    {
        public JsonElement? CreateUserBody { get; private set; }
        public string? ActionsEmailRequestUri { get; private set; }
        public JsonElement? ActionsEmailBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            var path = uri.AbsolutePath;

            if (path.EndsWith("/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { access_token = "admin-token" }),
                };
            }

            if (path.EndsWith("/execute-actions-email", StringComparison.Ordinal))
            {
                ActionsEmailRequestUri = uri.ToString();
                ActionsEmailBody = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.EndsWith("/users", StringComparison.Ordinal))
            {
                CreateUserBody = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
                var response = new HttpResponseMessage(createUserStatus);
                if (createUserStatus == HttpStatusCode.Created)
                {
                    response.Headers.Location =
                        new Uri($"https://keycloak.example/admin/realms/poc/users/{NewUserId}");
                }
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
