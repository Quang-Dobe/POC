using FluentAssertions;
using Poc.Bff.Application.Features.Auth.Me;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Auth.Me;

public class MeHandlerTests
{
    private readonly MeHandler _handler = new();

    [Fact]
    public async Task Handle_MapsClaimPrimitivesToMeResponse()
    {
        var query = new MeQuery("Ada Lovelace", new[] { "manager", "reader" });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Ada Lovelace");
        result.Value.Roles.Should().Equal("manager", "reader");
    }

    [Fact]
    public async Task Handle_EmptyRoles_StillSucceeds()
    {
        var query = new MeQuery("Bob", Array.Empty<string>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EmptyDisplayName_StillSucceeds()
    {
        var query = new MeQuery(string.Empty, new[] { "reader" });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().BeEmpty();
    }
}
