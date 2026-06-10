namespace Api.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Agents;
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

public class AgentEndpointsTests : IClassFixture<AgentEndpointsTests.GatedApiFactory>
{
    private const string AuthMarkerHeader = "X-Test-Authenticated";
    private const string TestScheme = "TestBearer";

    private readonly GatedApiFactory _factory;

    public AgentEndpointsTests(GatedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PostApiAsk_returns_401_without_authorization_header()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ask",
            new { question = "hello", session_id = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostApiAsk_returns_200_with_stub_answer_when_authenticated()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(AuthMarkerHeader, "true");

        var response = await client.PostAsJsonAsync("/api/ask",
            new { question = "hello", session_id = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AgentResponse>();
        Assert.NotNull(payload);
        Assert.Equal(StubAgentClient.StubAnswer, payload!.Answer);
    }

    public sealed class GatedApiFactory : WebApplicationFactory<Program>
    {
        public GatedApiFactory()
        {
            SecretStoreConfiguration.ReaderFactoryOverride = (_, _) => new FakeSecretStoreReader(
                new Dictionary<string, string?>
                {
                    ["Message:DisplayString"] = "fake-display-string",
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

                services.AddSingleton<IAgentGatewayClient, StubAgentClient>();
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

    internal sealed class StubAgentClient : IAgentGatewayClient
    {
        internal const string StubAnswer = "stub-answer-from-test";

        public Task<AgentResponse> AskAsync(
            string question,
            string? sessionId,
            string bearerToken,
            CancellationToken ct = default)
            => Task.FromResult(new AgentResponse(StubAnswer, sessionId ?? "stub-session"));
    }
}
