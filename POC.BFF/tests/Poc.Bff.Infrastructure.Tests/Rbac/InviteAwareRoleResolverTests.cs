namespace Poc.Bff.Infrastructure.Tests.Rbac;

using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Poc.Bff.Domain.Invites;
using Poc.Bff.Domain.Rbac;
using Poc.Bff.Infrastructure.Invites;
using Poc.Bff.Infrastructure.Rbac;
using Xunit;

public class InviteAwareRoleResolverTests
{
    private static InviteAwareRoleResolver Build(RoleMap roleMap, IInviteStore store)
    {
        var inner = new ConfigRoleResolver(Options.Create(roleMap), NullLogger<ConfigRoleResolver>.Instance);
        return new InviteAwareRoleResolver(inner, store, NullLogger<InviteAwareRoleResolver>.Instance);
    }

    private static RoleMap MapWithEntry() => new()
    {
        Entries = new Dictionary<string, RoleMapEntry>
        {
            ["manager-user"] = new RoleMapEntry { Roles = new[] { "manager" }, Region = "Oslo" },
        },
    };

    [Fact]
    public void EmptyStore_UnknownSubject_IsDenied()
    {
        var resolver = Build(MapWithEntry(), new InMemoryInviteStore());

        var resolution = resolver.Resolve("nobody-here");

        Assert.False(resolution.IsAllowed);
        Assert.Empty(resolution.Roles);
        Assert.Equal(string.Empty, resolution.Region);
    }

    [Fact]
    public void AfterAdd_InvitedSubject_IsAllowedWithDefaultRole()
    {
        var store = new InMemoryInviteStore();
        store.Add("erin", InviteGrant.Create(new[] { "reader" }, "Manhattan County"));
        var resolver = Build(MapWithEntry(), store);

        var resolution = resolver.Resolve("erin");

        Assert.True(resolution.IsAllowed);
        Assert.Equal(new[] { "reader" }, resolution.Roles);
        Assert.Equal("Manhattan County", resolution.Region);
    }

    [Fact]
    public void EmptyStore_KnownSubject_StillResolvesFromConfig()
    {
        var resolver = Build(MapWithEntry(), new InMemoryInviteStore());

        var resolution = resolver.Resolve("manager-user");

        Assert.True(resolution.IsAllowed);
        Assert.Equal(new[] { "manager" }, resolution.Roles);
        Assert.Equal("Oslo", resolution.Region);
    }

    [Fact]
    public void RoleMapEntry_StillWins_OverInvite()
    {
        var store = new InMemoryInviteStore();
        store.Add("manager-user", InviteGrant.Create(new[] { "reader" }, "Queens County"));
        var resolver = Build(MapWithEntry(), store);

        var resolution = resolver.Resolve("manager-user");

        Assert.True(resolution.IsAllowed);
        Assert.Equal(new[] { "manager" }, resolution.Roles);
        Assert.Equal("Oslo", resolution.Region);
    }
}
