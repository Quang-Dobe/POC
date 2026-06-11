using FluentAssertions;
using Poc.Bff.Application.Features.Auth.Callback;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Auth.Callback;

public class CallbackCommandValidatorTests
{
    private readonly CallbackCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        var command = new CallbackCommand(
            Code: "auth-code",
            State: "state-value",
            ExpectedState: "state-value",
            CodeVerifier: "verifier",
            Nonce: "nonce");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "state")]
    [InlineData(" ", "state")]
    [InlineData(null!, "state")]
    public void Validate_EmptyOrNullCode_FailsValidation(string? code, string state)
    {
        var command = new CallbackCommand(
            Code: code!,
            State: state,
            ExpectedState: state,
            CodeVerifier: "verifier",
            Nonce: "nonce");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CallbackCommand.Code));
    }

    [Theory]
    [InlineData("code", "")]
    [InlineData("code", " ")]
    [InlineData("code", null!)]
    public void Validate_EmptyOrNullState_FailsValidation(string code, string? state)
    {
        var command = new CallbackCommand(
            Code: code,
            State: state!,
            ExpectedState: "expected",
            CodeVerifier: "verifier",
            Nonce: "nonce");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CallbackCommand.State));
    }
}
