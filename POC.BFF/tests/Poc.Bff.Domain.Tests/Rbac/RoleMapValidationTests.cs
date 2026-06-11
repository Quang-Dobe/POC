namespace Poc.Bff.Domain.Tests.Rbac;

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Poc.Bff.Domain.Rbac;
using Xunit;

public class RoleMapValidationTests
{
    private static RoleMap BindValidated(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddOptions<RoleMap>()
            .Bind(config.GetSection(RoleMap.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<RoleMap>>().Value;
    }

    [Fact]
    public void Bind_BothDefaultsPresent_Validates_AndHasDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RoleMap:DefaultRoles:0"] = "reader",
                ["RoleMap:DefaultRegion"] = "Oslo",
            })
            .Build();

        var roleMap = BindValidated(config);

        Assert.True(roleMap.HasDefault);
        Assert.Equal(new[] { "reader" }, roleMap.DefaultRoles);
        Assert.Equal("Oslo", roleMap.DefaultRegion);
    }

    [Fact]
    public void Bind_NeitherDefault_Validates_DefaultDeny()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RoleMap:Entries:manager-user:Roles:0"] = "manager",
                ["RoleMap:Entries:manager-user:Region"] = "Oslo",
            })
            .Build();

        var roleMap = BindValidated(config);

        Assert.False(roleMap.HasDefault);
        Assert.Single(roleMap.Entries);
    }

    [Fact]
    public void Bind_DefaultRolesWithoutDefaultRegion_FailsValidationOnStart()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RoleMap:DefaultRoles:0"] = "reader",
            })
            .Build();

        Assert.Throws<OptionsValidationException>(() => BindValidated(config));
    }

    [Fact]
    public void Bind_DefaultRegionWithoutDefaultRoles_FailsValidationOnStart()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RoleMap:DefaultRegion"] = "Oslo",
            })
            .Build();

        Assert.Throws<OptionsValidationException>(() => BindValidated(config));
    }
}
