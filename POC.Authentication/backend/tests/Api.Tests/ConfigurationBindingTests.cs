namespace Api.Tests;

using Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    public void Bind_MessageOptions_NotValidated_AbsentDisplayStringBindsToNull()
    {

        var config = BuildConfiguration("DEV");

        var services = new ServiceCollection();
        services.AddOptions<MessageOptions>()
            .Bind(config.GetSection(MessageOptions.SectionName));

        using var provider = services.BuildServiceProvider();
        var message = provider.GetRequiredService<IOptions<MessageOptions>>().Value;

        Assert.Null(message.DisplayString);
    }

    [Fact]
    public void Bind_AgentOptions_Dev_HasClientIdAndTokenEndpoint()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.DEV.json", optional: false, reloadOnChange: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Host"] = "localhost",
                ["Agent:Port"] = "8082",
                ["Agent:ClientSecret"] = "test-secret",
            })
            .Build();

        var agent = BindValidated<AgentOptions>(config, AgentOptions.SectionName);

        Assert.Equal("poc-api", agent.ClientId);
        Assert.NotNull(agent.TokenEndpoint);
        Assert.Contains("/realms/poc/", agent.TokenEndpoint);
    }

    [Fact]
    public void Bind_AgentOptions_Prod_HasClientIdAndAgentScope()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.PROD.json", optional: false, reloadOnChange: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Host"] = "prod-agent.example.com",
                ["Agent:Port"] = "443",
                ["Agent:ClientSecret"] = "test-secret",
            })
            .Build();

        var agent = BindValidated<AgentOptions>(config, AgentOptions.SectionName);

        Assert.NotEmpty(agent.ClientId);
        Assert.NotNull(agent.AgentScope);
        Assert.StartsWith("api://", agent.AgentScope);
    }

    [Fact]
    public void Bind_AgentOptions_MissingClientId_FailsValidation()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Host"] = "localhost",
                ["Agent:Port"] = "8082",
                ["Agent:ClientSecret"] = "s",
            })
            .Build();

        Assert.Throws<OptionsValidationException>(
            () => BindValidated<AgentOptions>(config, AgentOptions.SectionName));
    }
}
