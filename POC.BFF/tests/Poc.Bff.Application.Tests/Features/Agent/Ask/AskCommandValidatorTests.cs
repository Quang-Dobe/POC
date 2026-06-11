using FluentAssertions;
using Poc.Bff.Application.Features.Agent.Ask;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Agent.Ask;

public class AskCommandValidatorTests
{
    private readonly AskCommandValidator _validator = new();

    [Fact]
    public async Task Validate_EmptyQuestion_FailsValidation()
    {
        var command = new AskCommand(
            Question: "",
            SessionId: null,
            Sub: "sub",
            Roles: Array.Empty<string>(),
            Region: null);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(AskCommand.Question));
    }

    [Fact]
    public async Task Validate_WhitespaceOnlyQuestion_FailsValidation()
    {
        var command = new AskCommand(
            Question: "   ",
            SessionId: null,
            Sub: "sub",
            Roles: Array.Empty<string>(),
            Region: null);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ValidQuestion_Passes()
    {
        var command = new AskCommand(
            Question: "What is the capital of Norway?",
            SessionId: null,
            Sub: "sub",
            Roles: Array.Empty<string>(),
            Region: null);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }
}
