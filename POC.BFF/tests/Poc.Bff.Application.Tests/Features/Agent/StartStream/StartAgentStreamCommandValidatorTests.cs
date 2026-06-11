using FluentAssertions;
using Poc.Bff.Application.Features.Agent.StartStream;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Agent.StartStream;

public class StartAgentStreamCommandValidatorTests
{
    private readonly StartAgentStreamCommandValidator _validator = new();

    [Fact]
    public async Task Validate_EmptyQuestion_FailsValidation()
    {
        var command = new StartAgentStreamCommand(
            Question: "",
            SessionId: null,
            Sub: "sub",
            Roles: Array.Empty<string>(),
            Region: null);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(StartAgentStreamCommand.Question));
    }

    [Fact]
    public async Task Validate_WhitespaceOnlyQuestion_FailsValidation()
    {
        var command = new StartAgentStreamCommand(
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
        var command = new StartAgentStreamCommand(
            Question: "Stream this question",
            SessionId: null,
            Sub: "sub",
            Roles: Array.Empty<string>(),
            Region: null);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }
}
