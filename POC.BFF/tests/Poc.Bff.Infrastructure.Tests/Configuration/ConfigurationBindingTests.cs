namespace Poc.Bff.Infrastructure.Tests.Configuration;

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Features.Message;
using Poc.Bff.Infrastructure.Configuration;
using Xunit;

public class ConfigurationBindingTests
{
    private static IConfiguration BuildConfiguration(string env) =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{env}.json", optional: false, reloadOnChange: false)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ENV"] = env })
            .Build();

    private static T BindValidated<T>(IConfiguration config, string section) where T : class
    {
        var services = new ServiceCollection();
        services.AddOptions<T>()
            .Bind(config.GetSection(section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<T>>().Value;
    }

    [Fact]
    public void Bind_EnvDev_ResolvesKeycloakShapedAuthority()
    {
        var config = BuildConfiguration("DEV");

        var auth = BindValidated<AuthOptions>(config, AuthOptions.SectionName);

        Assert.Equal("https://localhost:8080/realms/poc", auth.Authority);
        Assert.Contains("/realms/", auth.Authority);
        Assert.DoesNotContain("/v2.0", auth.Authority);
        Assert.Equal("poc-api", auth.Audience);
    }

    [Fact]
    public void Bind_EnvProd_ResolvesEntraV2ShapedAuthority()
    {
        var config = BuildConfiguration("PROD");

        var auth = BindValidated<AuthOptions>(config, AuthOptions.SectionName);

        Assert.EndsWith("/v2.0", auth.Authority);
        Assert.StartsWith("https://login.microsoftonline.com/", auth.Authority);
        Assert.DoesNotContain("/realms/", auth.Authority);
        Assert.StartsWith("api://", auth.Audience);
    }

    [Theory]
    [InlineData("DEV", "http://localhost:5173")]
    [InlineData("PROD", "https://<spa-origin>")]
    public void Bind_Cors_ResolvesPerModeSpaOriginFromSameSwitch(string env, string expectedOrigin)
    {
        var config = BuildConfiguration(env);

        var cors = BindValidated<CorsOptions>(config, CorsOptions.SectionName);

        Assert.Equal(expectedOrigin, cors.SpaOrigin);
    }

    [Fact]
    public void Bind_AuthOptions_MissingRequiredValue_FailsValidationOnStart()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<OptionsValidationException>(
            () => BindValidated<AuthOptions>(config, AuthOptions.SectionName));
    }

    [Fact]
    public void Bind_MessageOptions_NotValidated_AbsentDisplayStringBindsToEmpty()
    {

        var config = BuildConfiguration("DEV");

        var services = new ServiceCollection();
        services.AddOptions<MessageOptions>()
            .Bind(config.GetSection(MessageOptions.SectionName));

        using var provider = services.BuildServiceProvider();
        var message = provider.GetRequiredService<IOptions<MessageOptions>>().Value;

        Assert.NotNull(message.DisplayString);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("PROD")]
    public void Bind_AgentOptions_BindsHostAndPort_PostPrune(string env)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile($"appsettings.{env}.json", optional: false, reloadOnChange: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Host"] = "localhost",
                ["Agent:Port"] = "8082",
            })
            .Build();

        var agent = BindValidated<AgentOptions>(config, AgentOptions.SectionName);

        Assert.Equal("localhost", agent.Host);
        Assert.Equal(8082, agent.Port);
    }

    [Fact]
    public void Bind_AgentOptions_MissingHost_FailsValidation()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Port"] = "8082",
            })
            .Build();

        Assert.Throws<OptionsValidationException>(
            () => BindValidated<AgentOptions>(config, AgentOptions.SectionName));
    }

    [Theory]
    [InlineData("DEV", "localhost:6379")]
    [InlineData("PROD", "<redis-host>:6380")]
    public void Bind_Session_ResolvesPerModeRequiredMembers(string env, string expectedRedisAddress)
    {
        var config = BuildConfiguration(env);

        var session = BindValidated<SessionOptions>(config, SessionOptions.SectionName);

        Assert.Equal("poc.session", session.CookieName);
        Assert.Equal(30, session.TtlMinutes);
        Assert.Equal(expectedRedisAddress, session.RedisAddress);
    }

    [Fact]
    public void Bind_Session_MissingRequiredMembers_FailsValidationOnStart()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<OptionsValidationException>(
            () => BindValidated<SessionOptions>(config, SessionOptions.SectionName));
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("PROD")]
    public void Bind_DataProtection_ResolvesApplicationNameAndRedisKey(string env)
    {
        var config = BuildConfiguration(env);

        var dp = BindValidated<DataProtectionOptions>(config, DataProtectionOptions.SectionName);

        Assert.Equal("poc-auth", dp.ApplicationName);
        Assert.Equal("dp:poc-keyring", dp.RedisKey);
    }

    [Fact]
    public void Bind_DataProtection_MissingRequiredMembers_FailsValidationOnStart()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<OptionsValidationException>(
            () => BindValidated<DataProtectionOptions>(config, DataProtectionOptions.SectionName));
    }

    [Fact]
    public void Bind_IdpSimulator_Dev_ResolvesSharedIssuerAndAudience()
    {
        var config = BuildConfiguration("DEV");

        var idp = BindValidated<IdpSimulatorOptions>(config, IdpSimulatorOptions.SectionName);

        Assert.Equal("https://localhost:5001", idp.Issuer);
        Assert.Contains("://", idp.Issuer);
        Assert.Equal("poc-internal", idp.Audience);
        Assert.Equal(300, idp.DownstreamTokenTtlSeconds);
        Assert.Equal("poc-idp-key-1", idp.SigningKeyId);
        Assert.Null(idp.SigningKeyPem);
    }

    [Fact]
    public void Bind_IdpSimulator_Prod_ResolvesSharedIssuerAndAudience()
    {
        var config = BuildConfiguration("PROD");

        var idp = BindValidated<IdpSimulatorOptions>(config, IdpSimulatorOptions.SectionName);

        Assert.Equal("https://<bff-origin>", idp.Issuer);
        Assert.Contains("://", idp.Issuer);
        Assert.Equal("poc-internal", idp.Audience);
        Assert.Equal("poc-idp-key-1", idp.SigningKeyId);
    }

    [Fact]
    public void Bind_IdpSimulator_MissingRequiredMembers_FailsValidationOnStart()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<OptionsValidationException>(
            () => BindValidated<IdpSimulatorOptions>(config, IdpSimulatorOptions.SectionName));
    }

    [Theory]
    [InlineData("DEV", "poc")]
    [InlineData("PROD", "<entra-tenant-id>")]
    public void Bind_Invite_ResolvesDefaultRoleRegionAndTenant(string env, string expectedTenant)
    {
        var config = BuildConfiguration(env);

        var invite = BindValidated<InviteOptions>(config, InviteOptions.SectionName);

        Assert.Equal(new[] { "reader" }, invite.DefaultRoles);
        Assert.Equal("Manhattan County", invite.DefaultRegion);
        Assert.Equal(expectedTenant, invite.TenantId);
    }

    [Fact]
    public void Bind_Invite_HalfConfiguredDefaultPair_FailsValidationOnStart()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Invite:DefaultRoles:0"] = "reader",
                ["Invite:TenantId"] = "poc",
            })
            .Build();

        Assert.Throws<OptionsValidationException>(
            () => BindValidated<InviteOptions>(config, InviteOptions.SectionName));
    }

    [Fact]
    public void Bind_Invite_MissingTenantId_FailsValidationOnStart()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<OptionsValidationException>(
            () => BindValidated<InviteOptions>(config, InviteOptions.SectionName));
    }

    [Theory]
    [InlineData("DEV", "https://localhost:8080")]
    [InlineData("PROD", "https://<keycloak-origin>")]
    public void Bind_KeycloakAdmin_ResolvesNonSecretMembers_SecretAbsent(string env, string expectedBaseUrl)
    {
        var config = BuildConfiguration(env);

        var admin = BindValidated<KeycloakAdminOptions>(config, KeycloakAdminOptions.SectionName);

        Assert.Equal(expectedBaseUrl, admin.AdminBaseUrl);
        Assert.Equal("poc", admin.Realm);
        Assert.Equal("poc-admin-cli", admin.AdminClientId);
        Assert.Null(admin.AdminClientSecret);
    }

    [Fact]
    public void Bind_KeycloakAdmin_MissingRequiredMembers_FailsValidationOnStart()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<OptionsValidationException>(
            () => BindValidated<KeycloakAdminOptions>(config, KeycloakAdminOptions.SectionName));
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("PROD")]
    public void Bind_RoleMap_FromLinkedAppsettings_ValidatesInBothModes(string env)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile($"appsettings.{env}.json", optional: false, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<Domain.Rbac.RoleMap>()
            .Bind(config.GetSection(Domain.Rbac.RoleMap.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider();
        var roleMap = provider.GetRequiredService<IOptions<Domain.Rbac.RoleMap>>().Value;

        Assert.NotNull(roleMap);
        Assert.False(roleMap.HasDefault);
    }
}
