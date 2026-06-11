using FluentAssertions;
using NSubstitute;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Features.Auth.Login;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Auth.Login;

public class LoginHandlerTests
{
    private readonly IOidcAuthClient _oidcAuthClient = Substitute.For<IOidcAuthClient>();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _handler = new LoginHandler(_oidcAuthClient);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WithExpectedAuthorizeUrl()
    {
        const string expectedUrl = "https://idp.example/authorize?state=abc";

        _oidcAuthClient
            .BuildAuthorizeUrlAsync(Arg.Any<OidcAuthCorrelation>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedUrl));

        var result = await _handler.Handle(new LoginQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorizeUrl.Should().Be(expectedUrl);
    }

    [Fact]
    public async Task Handle_PopulatesCorrelationFields_NonEmpty()
    {
        _oidcAuthClient
            .BuildAuthorizeUrlAsync(Arg.Any<OidcAuthCorrelation>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://idp.example/authorize"));

        var result = await _handler.Handle(new LoginQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().NotBeNullOrWhiteSpace();
        result.Value.CodeVerifier.Should().NotBeNullOrWhiteSpace();
        result.Value.Nonce.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_PassesCorrelationToOidcClient()
    {
        OidcAuthCorrelation? captured = null;

        _oidcAuthClient
            .BuildAuthorizeUrlAsync(Arg.Do<OidcAuthCorrelation>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://idp.example/authorize"));

        var result = await _handler.Handle(new LoginQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.State.Should().Be(result.Value.State);
        captured.CodeVerifier.Should().Be(result.Value.CodeVerifier);
        captured.Nonce.Should().Be(result.Value.Nonce);
    }

    [Fact]
    public async Task Handle_EachCall_GeneratesUniqueState()
    {
        _oidcAuthClient
            .BuildAuthorizeUrlAsync(Arg.Any<OidcAuthCorrelation>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("https://idp.example/authorize"));

        var first = await _handler.Handle(new LoginQuery(), CancellationToken.None);
        var second = await _handler.Handle(new LoginQuery(), CancellationToken.None);

        first.Value.State.Should().NotBe(second.Value.State);
    }
}
