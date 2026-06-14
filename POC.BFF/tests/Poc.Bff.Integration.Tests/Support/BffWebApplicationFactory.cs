using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Poc.Bff.Integration.Tests.Support;

public class BffWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _signingKeyPem = TestRsaKey.NewPrivatePem();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ENV", "DEV");

        builder.UseSetting("DisableSecretStore", "true");

        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(BuildCommonConfig()));

        builder.ConfigureServices(services =>
        {

            CookieTestSupport.UseEphemeralDataProtection(services);

            TestRedis.SubstituteMultiplexer(services);
        });
    }

    protected string SigningKeyPem => _signingKeyPem;

    private Dictionary<string, string?> BuildCommonConfig() => new()
    {

        ["Message:DisplayString"] = "integration-test-message",

        ["Session:CookieName"] = "poc.session",
        ["Session:TtlMinutes"] = "60",
        ["Session:RedisAddress"] = "localhost:6379",

        ["Cors:SpaOrigin"] = "http://localhost:3000",

        ["IdpSimulator:Issuer"] = "https://poc-bff-test.local",
        ["IdpSimulator:Audience"] = "poc-bff",
        ["IdpSimulator:DownstreamTokenTtlSeconds"] = "3600",
        ["IdpSimulator:SigningKeyId"] = "test-signing-key-1",
        ["IdpSimulator:SigningKeyPem"] = _signingKeyPem,

        ["Auth:Authority"] = "https://idp.test.local",
        ["Auth:Audience"] = "poc-bff",
        ["Auth:ClientId"] = "poc-bff-client",
        ["Auth:RedirectUri"] = "https://bff.test.local/auth/callback",
        ["Auth:FrontendReturnUrl"] = "https://bff.test.local",

        ["DataProtection:ApplicationName"] = "poc-bff-test",
        ["DataProtection:RedisKey"] = "dp:keys:test",
        ["DataProtection:MasterKey"] = "test-master-key-32-bytes-padding!",

        ["Invite:TenantId"] = "test-tenant-id",
        ["Invite:DefaultPassword"] = "Test1234!",

        ["Keycloak:AdminBaseUrl"] = "https://keycloak.test.local",
        ["Keycloak:Realm"] = "test-realm",
        ["Keycloak:AdminClientId"] = "admin-cli",

        ["Agent:Host"] = "localhost",
        ["Agent:Port"] = "8082",

        ["Streaming:ExtendLeadSeconds"] = "60",
        ["Streaming:JitterSeconds"] = "10",
    };
}
