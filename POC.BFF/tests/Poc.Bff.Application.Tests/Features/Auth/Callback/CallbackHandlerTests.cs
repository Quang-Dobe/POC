using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Features.Auth;
using Poc.Bff.Application.Features.Auth.Callback;
using Poc.Bff.Domain.Rbac;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Auth.Callback;

public class CallbackHandlerTests
{
    private const string FrontendReturnUrl = "https://app.example";
    private const string ValidCode = "valid-auth-code";
    private const string ValidState = "state-abc";
    private const string ValidCodeVerifier = "verifier-xyz";
    private const string ValidNonce = "nonce-123";
    private const string Subject = "user-sub";
    private const string DisplayName = "Ada Lovelace";
    private const string Region = "Oslo";

    private readonly IOidcAuthClient _oidcAuthClient = Substitute.For<IOidcAuthClient>();
    private readonly IRoleResolver _roleResolver = Substitute.For<IRoleResolver>();
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly CallbackHandler _handler;

    public CallbackHandlerTests()
    {
        _authOptions = Options.Create(new AuthOptions
        {
            Authority = "https://idp.example",
            ClientId = "bff-client",
            RedirectUri = "https://bff.example/auth/callback",
            FrontendReturnUrl = FrontendReturnUrl,
        });

        _handler = new CallbackHandler(_oidcAuthClient, _roleResolver, _authOptions);
    }

    [Fact]
    public async Task Handle_ValidStateAndAllowed_ReturnsSuccess_WithExpectedSession()
    {
        _oidcAuthClient
            .ExchangeCodeAsync(ValidCode, Arg.Any<OidcAuthCorrelation>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ExternalIdentity(Subject, DisplayName)));

        _roleResolver
            .Resolve(Subject)
            .Returns(RoleResolution.Allowed(new[] { "manager" }, Region));

        var command = BuildCommand(code: ValidCode, state: ValidState, expectedState: ValidState);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Subject.Should().Be(Subject);
        result.Value.DisplayName.Should().Be(DisplayName);
        result.Value.Region.Should().Be(Region);
        result.Value.Roles.Should().Equal("manager");
        result.Value.RedirectUrl.Should().Be(FrontendReturnUrl);
    }

    [Fact]
    public async Task Handle_ValidStateAndAllowed_PassesCorrelationToOidcClient()
    {

        OidcAuthCorrelation? captured = null;
        _oidcAuthClient
            .ExchangeCodeAsync(
                Arg.Any<string>(),
                Arg.Do<OidcAuthCorrelation>(c => captured = c),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ExternalIdentity(Subject, DisplayName)));

        _roleResolver
            .Resolve(Arg.Any<string>())
            .Returns(RoleResolution.Allowed(new[] { "reader" }, Region));

        var command = BuildCommand(code: ValidCode, state: ValidState, expectedState: ValidState);

        await _handler.Handle(command, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.State.Should().Be(ValidState);
        captured.CodeVerifier.Should().Be(ValidCodeVerifier);
        captured.Nonce.Should().Be(ValidNonce);
    }

    [Fact]
    public async Task Handle_StateMismatch_ReturnsFailure_WithStateMismatchError()
    {

        var command = BuildCommand(code: ValidCode, state: "tampered-state", expectedState: ValidState);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthErrors.StateMismatch.Code);
    }

    [Fact]
    public async Task Handle_StateMismatch_DoesNotCallOidcClient()
    {

        var command = BuildCommand(code: ValidCode, state: "wrong", expectedState: ValidState);

        await _handler.Handle(command, CancellationToken.None);

        await _oidcAuthClient
            .DidNotReceive()
            .ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<OidcAuthCorrelation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OidcClientThrows_ReturnsFailure_WithCodeExchangeFailedError()
    {

        _oidcAuthClient
            .ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<OidcAuthCorrelation>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OidcAuthException("IDP token exchange failed."));

        var command = BuildCommand(code: ValidCode, state: ValidState, expectedState: ValidState);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthErrors.CodeExchangeFailed.Code);
    }

    [Fact]
    public async Task Handle_RoleResolverDenied_ReturnsFailure_WithAccessDeniedError()
    {

        _oidcAuthClient
            .ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<OidcAuthCorrelation>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ExternalIdentity(Subject, DisplayName)));

        _roleResolver
            .Resolve(Arg.Any<string>())
            .Returns(RoleResolution.Denied);

        var command = BuildCommand(code: ValidCode, state: ValidState, expectedState: ValidState);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthErrors.AccessDenied.Code);
    }

    private static CallbackCommand BuildCommand(string code, string state, string expectedState) =>
        new(
            Code: code,
            State: state,
            ExpectedState: expectedState,
            CodeVerifier: ValidCodeVerifier,
            Nonce: ValidNonce);
}
