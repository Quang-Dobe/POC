using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Poc.Bff.Integration.Tests.Support;
using Xunit;

namespace Poc.Bff.Integration.Tests;

public class WellKnownEndpointTests : IClassFixture<WellKnownEndpointTests.WellKnownFactory>
{
    private const string TestIssuer = "https://poc-bff-test.local";
    private const string TestKid = "test-signing-key-1";

    private readonly WellKnownFactory _factory;

    public WellKnownEndpointTests(WellKnownFactory factory) => _factory = factory;

    [Fact]
    public async Task GetJwks_Returns200()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetJwks_IsAnonymous_NoCookieRequired()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        var response = await client.GetAsync("/.well-known/jwks.json");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetJwks_BodyHasKeysArray_WithAtLeastOneKey()
    {
        using var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/.well-known/jwks.json");

        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.GetProperty("keys").EnumerateArray().ToList();
        Assert.True(keys.Count >= 1, "Expected at least one key in the JWKS.");
    }

    [Fact]
    public async Task GetJwks_KeyHasConfiguredKid()
    {
        using var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/.well-known/jwks.json");

        using var doc = JsonDocument.Parse(json);
        var kids = doc.RootElement
            .GetProperty("keys")
            .EnumerateArray()
            .Select(k => k.GetProperty("kid").GetString())
            .ToList();

        Assert.Contains(TestKid, kids);
    }

    [Fact]
    public async Task GetJwks_KeyHasRsaKtyAndRs256Alg()
    {
        using var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/.well-known/jwks.json");

        using var doc = JsonDocument.Parse(json);
        foreach (var key in doc.RootElement.GetProperty("keys").EnumerateArray())
        {
            Assert.Equal("RSA", key.GetProperty("kty").GetString());
            Assert.Equal("RS256", key.GetProperty("alg").GetString());
        }
    }

    [Fact]
    public async Task GetOpenIdConfiguration_Returns200()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOpenIdConfiguration_IsAnonymous_NoCookieRequired()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOpenIdConfiguration_IssuerMatchesConfigured()
    {
        using var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/.well-known/openid-configuration");

        using var doc = JsonDocument.Parse(json);
        var issuer = doc.RootElement.GetProperty("issuer").GetString();
        Assert.Equal(TestIssuer, issuer);
    }

    [Fact]
    public async Task GetOpenIdConfiguration_JwksUriEndsWithWellKnownJwks()
    {
        using var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/.well-known/openid-configuration");

        using var doc = JsonDocument.Parse(json);
        var jwksUri = doc.RootElement.GetProperty("jwks_uri").GetString();
        Assert.NotNull(jwksUri);
        Assert.EndsWith("/.well-known/jwks.json", jwksUri);
    }

    public sealed class WellKnownFactory : BffWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {

            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{IdpSimulatorSection}:Issuer"] = TestIssuer,
                    [$"{IdpSimulatorSection}:SigningKeyId"] = TestKid,
                }));
        }

        private const string IdpSimulatorSection = "IdpSimulator";
    }
}
