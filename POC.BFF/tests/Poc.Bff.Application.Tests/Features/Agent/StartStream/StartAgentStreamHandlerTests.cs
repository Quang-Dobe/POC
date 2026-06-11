using FluentAssertions;
using NSubstitute;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Features.Agent.StartStream;
using Poc.Bff.Domain.Tokens;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Agent.StartStream;

public class StartAgentStreamHandlerTests
{
    private const string StubToken = "minted-stream-token";

    private readonly IDownstreamTokenMinter _minter = Substitute.For<IDownstreamTokenMinter>();
    private readonly IAgentGatewayClient _agent = Substitute.For<IAgentGatewayClient>();

    public StartAgentStreamHandlerTests()
    {
        _minter.Mint(Arg.Any<DownstreamClaims>()).Returns(StubToken);
    }

    [Fact]
    public async Task Handle_MintsTokenAndReturnsAgentStream()
    {
        var stream = Stream("frame-1", "frame-2");
        _agent.AskStreamAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(stream);

        var handler = new StartAgentStreamHandler(_minter, _agent);
        var command = new StartAgentStreamCommand(
            Question: "Tell me about streaming",
            SessionId: "sess-abc",
            Sub: "user-sub",
            Roles: new[] { "manager" },
            Region: "Bergen");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(stream);

        _agent.Received(1).AskStreamAsync("Tell me about streaming", "sess-abc", StubToken, Arg.Any<CancellationToken>());
        _minter.Received(1).Mint(Arg.Is<DownstreamClaims>(c =>
            c.Sub == "user-sub" &&
            c.Region == "Bergen" &&
            c.Roles != null && c.Roles.Contains("manager")));
    }

#pragma warning disable CS1998
    private static async IAsyncEnumerable<string> Stream(params string[] frames)
    {
        foreach (var frame in frames)
        {
            yield return frame;
        }
    }
#pragma warning restore CS1998
}
