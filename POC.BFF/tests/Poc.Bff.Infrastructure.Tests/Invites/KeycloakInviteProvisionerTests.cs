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

public class KeycloakInviteProvisionerTests
{
    private const string DefaultPassword = "Test1234!";

    private static (KeycloakInviteProvisioner provisioner, RoutingHandler handler) Build(
        HttpStatusCode createUserStatus = HttpStatusCode.Created,
        string? adminSecret = "admin-secret",
        string? defaultPassword = DefaultPassword)
    {
        var handler = new RoutingHandler(createUserStatus);
        var factory = new StubHttpClientFactory(handler);

        var adminOptions = Options.Create(new KeycloakAdminOptions
        {
            AdminBaseUrl = "https://keycloak.example",
            Realm = "poc",
            AdminClientId = "poc-admin-cli",
            AdminClientSecret = adminSecret,
        });

        var inviteOptions = Options.Create(new InviteOptions
        {
            DefaultRoles = new[] { "reader" },
            DefaultRegion = "Manhattan County",
            DefaultPassword = defaultPassword,
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
    public async Task ProvisionAsync_CreatesUser_WithSecretStoreDefaultPassword_NotTemporary()
    {
        var (provisioner, handler) = Build();

        var outcome = await provisioner.ProvisionAsync(new InviteRequest("erin@example.com"));

        Assert.Equal("erin@example.com", outcome.Subject);
        Assert.False(outcome.AlreadyExisted);
        Assert.True(outcome.PasswordSet);

        Assert.NotNull(handler.CreateUserBody);
        var body = handler.CreateUserBody!.Value;
        Assert.True(body.GetProperty("emailVerified").GetBoolean());
        var credential = body.GetProperty("credentials")[0];
        Assert.Equal("password", credential.GetProperty("type").GetString());
        Assert.Equal(DefaultPassword, credential.GetProperty("value").GetString());
        Assert.False(credential.GetProperty("temporary").GetBoolean());

        // No emailed action link is triggered under the default-password flow.
        Assert.False(handler.SawActionsEmail);
    }

    [Fact]
    public async Task ProvisionAsync_WhenUserAlreadyExists_ReturnsAlreadyExisted_WithoutPasswordSet()
    {
        var (provisioner, _) = Build(createUserStatus: HttpStatusCode.Conflict);

        var outcome = await provisioner.ProvisionAsync(new InviteRequest("erin@example.com"));

        Assert.True(outcome.AlreadyExisted);
        Assert.False(outcome.PasswordSet);
    }

    [Fact]
    public async Task ProvisionAsync_WhenDefaultPasswordMissing_ThrowsInviteException()
    {
        var (provisioner, _) = Build(defaultPassword: null);

        await Assert.ThrowsAsync<InviteException>(
            () => provisioner.ProvisionAsync(new InviteRequest("erin@example.com")));
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
        public bool SawActionsEmail { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { access_token = "admin-token" }),
                };
            }

            if (path.EndsWith("/execute-actions-email", StringComparison.Ordinal))
            {
                SawActionsEmail = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.EndsWith("/users", StringComparison.Ordinal))
            {
                CreateUserBody = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
                return new HttpResponseMessage(createUserStatus);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
