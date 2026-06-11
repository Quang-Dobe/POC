namespace Poc.Bff.Infrastructure.Tests.Rbac;

using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Poc.Bff.Domain.Rbac;
using Poc.Bff.Infrastructure.Rbac;
using Xunit;

public class ConfigRoleResolverTests
{
    private static ConfigRoleResolver Build(RoleMap roleMap) =>
        new(Options.Create(roleMap), NullLogger<ConfigRoleResolver>.Instance);

    private static RoleMap MapWithEntry() => new()
    {
        Entries = new Dictionary<string, RoleMapEntry>
        {
            ["manager-user"] = new RoleMapEntry { Roles = new[] { "manager" }, Region = "Oslo" },
        },
    };

    [Fact]
    public void Resolve_KnownSubject_ReturnsMappedRolesAndRegion()
    {
        var resolver = Build(MapWithEntry());

        var resolution = resolver.Resolve("manager-user");

        Assert.True(resolution.IsAllowed);
        Assert.Equal(new[] { "manager" }, resolution.Roles);
        Assert.Equal("Oslo", resolution.Region);
    }

    [Fact]
    public void Resolve_UnknownSubject_NoDefault_IsDenied()
    {
        var resolver = Build(MapWithEntry());

        var resolution = resolver.Resolve("nobody-here");

        Assert.False(resolution.IsAllowed);
        Assert.Empty(resolution.Roles);
        Assert.Equal(string.Empty, resolution.Region);
    }

    [Fact]
    public void Resolve_UnknownSubject_WithDefaultReader_ReturnsTheDefault()
    {
        var resolver = Build(new RoleMap
        {
            Entries = new Dictionary<string, RoleMapEntry>(),
            DefaultRoles = new[] { "reader" },
            DefaultRegion = "Oslo",
        });

        var resolution = resolver.Resolve("nobody-here");

        Assert.True(resolution.IsAllowed);
        Assert.Equal(new[] { "reader" }, resolution.Roles);
        Assert.Equal("Oslo", resolution.Region);
    }

    [Fact]
    public void Resolve_KnownSubject_TakesPrecedenceOverTheDefaultReader()
    {
        var resolver = Build(new RoleMap
        {
            Entries = new Dictionary<string, RoleMapEntry>
            {
                ["manager-user"] = new RoleMapEntry { Roles = new[] { "manager" }, Region = "Bergen" },
            },
            DefaultRoles = new[] { "reader" },
            DefaultRegion = "Oslo",
        });

        var resolution = resolver.Resolve("manager-user");

        Assert.True(resolution.IsAllowed);
        Assert.Equal(new[] { "manager" }, resolution.Roles);
        Assert.Equal("Bergen", resolution.Region);
    }

    [Fact]
    public void Resolve_EntryMissingRegion_IsDenied_NotCrashOnMint()
    {
        var resolver = Build(new RoleMap
        {
            Entries = new Dictionary<string, RoleMapEntry>
            {
                ["broken"] = new RoleMapEntry { Roles = new[] { "manager" }, Region = " " },
            },
        });

        var resolution = resolver.Resolve("broken");

        Assert.False(resolution.IsAllowed);
    }
}
