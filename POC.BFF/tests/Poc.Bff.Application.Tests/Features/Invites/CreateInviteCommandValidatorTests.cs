using FluentAssertions;
using Poc.Bff.Application.Features.Invites.CreateInvite;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Invites;

public class CreateInviteCommandValidatorTests
{
    private readonly CreateInviteCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        var command = new CreateInviteCommand(Username: "erin@example.com");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null!)]
    public void Validate_EmptyOrNullUsername_FailsValidation(string? username)
    {
        var command = new CreateInviteCommand(Username: username!);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateInviteCommand.Username));
    }
}
