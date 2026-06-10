namespace Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

public class MessageEndpointTests : IClassFixture<MessageEndpointTests.GatedApiFactory>
{

    private const string SeededDisplayString = "seeded-from-secret-store-abc123";

    private const string AuthMarkerHeader = "X-Test-Authenticated";

    private const string TestScheme = "TestBearer";

    private readonly GatedApiFactory _factory;

    public MessageEndpointTests(GatedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMessage_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/message");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SeededDisplayString, body);
    }

    [Fact]
    public async Task GetMessage_WithToken_Returns200WithSeededDisplayString()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(AuthMarkerHeader, "true");

        var response = await client.GetAsync("/api/message");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(payload);
        Assert.Equal(SeededDisplayString, payload!.Message);
    }

    private sealed record MessageResponse(string Message);

    public sealed class GatedApiFactory : WebApplicationFactory<Program>
    {
        public GatedApiFactory()
        {

            SecretStoreConfiguration.ReaderFactoryOverride = (_, _) => new FakeSecretStoreReader(
                new Dictionary<string, string?>
                {
                    ["Message:DisplayString"] = SeededDisplayString,
                });
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {

            builder.UseSetting("ENV", "DEV");
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agent:Host"] = "localhost",
                    ["Agent:Port"] = "9999",
                    ["Agent:ClientSecret"] = "test-secret",
                }));

            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

                SecretStoreConfiguration.ReaderFactoryOverride = null;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class FakeSecretStoreReader : ISecretStoreReader
    {
        private readonly IReadOnlyDictionary<string, string?> _secrets;

        internal FakeSecretStoreReader(IReadOnlyDictionary<string, string?> secrets) =>
            _secrets = secrets;

        public IReadOnlyDictionary<string, string?> Load() => _secrets;
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(AuthMarkerHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, "test-user") },
                TestScheme);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
