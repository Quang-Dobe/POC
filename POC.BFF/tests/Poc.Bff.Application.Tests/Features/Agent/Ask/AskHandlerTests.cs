using FluentAssertions;
using NSubstitute;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Features.Agent.Ask;
using Poc.Bff.Domain.Tokens;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Agent.Ask;

public class AskHandlerTests
{
    private const string StubToken = "minted-token-stub";
    private const string StubAnswer = "stub-answer";
    private const string StubSessionId = "session-123";

    private readonly IDownstreamTokenMinter _minter = Substitute.For<IDownstreamTokenMinter>();
    private readonly IAgentGatewayClient _agent = Substitute.For<IAgentGatewayClient>();

    public AskHandlerTests()
    {
        _minter.Mint(Arg.Any<DownstreamClaims>()).Returns(StubToken);
        _agent.AskAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AgentResponse(StubAnswer, StubSessionId));
    }

    [Fact]
    public async Task Handle_MintsTokenFromClaimsAndCallsAgent_ReturnsSuccessWithResponse()
    {
        var handler = new AskHandler(_minter, _agent);
        var command = new AskCommand(
            Question: "What is AI?",
            SessionId: StubSessionId,
            Sub: "user-sub",
            Roles: new[] { "reader" },
            Region: "Oslo");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Answer.Should().Be(StubAnswer);

        _minter.Received(1).Mint(Arg.Is<DownstreamClaims>(c =>
            c.Sub == "user-sub" &&
            c.Region == "Oslo" &&
            c.Roles != null && c.Roles.Contains("reader")));

        await _agent.Received(1).AskAsync(
            "What is AI?",
            StubSessionId,
            StubToken,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesMintedTokenToAgent_NotTheInputToken()
    {
        var handler = new AskHandler(_minter, _agent);
        var command = new AskCommand(
            Question: "hello",
            SessionId: null,
            Sub: "user-sub",
            Roles: Array.Empty<string>(),
            Region: null);

        await handler.Handle(command, CancellationToken.None);

        await _agent.Received(1).AskAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            StubToken,
            Arg.Any<CancellationToken>());
    }
}
